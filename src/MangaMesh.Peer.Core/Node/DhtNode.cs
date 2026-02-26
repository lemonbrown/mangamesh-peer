using System.Text;
using System.Text.Json;
using MangaMesh.Shared.Configuration;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Core.Node
{
    public class DhtNode : IDhtNode
    {
        public struct ProviderInfo
        {
            public byte[] NodeId;
            public NodeAddress Address;
        }

        private readonly IKeyPairService _keypairService;
        private readonly IKeyStore _keyStore;
        private readonly IRoutingTable _routingTable;
        private readonly IBootstrapNodeProvider _bootstrapNodeProvider;
        private readonly IDhtRequestTracker _requestTracker;
        private readonly INodeConnectionInfoProvider _connectionInfo;
        private readonly ITransportSelector _transportSelector;
        private readonly bool _supportsWebRtc;
        private readonly ILogger<DhtNode> _logger;

        private readonly IDhtMessageHandler _messageHandler;
        private readonly IDhtLookupService _lookupService;

        private bool _running;
        private int _httpApiPort;
        private bool _httpApiPortResolved;

        private readonly List<RoutingEntry> _knownBootstrapNodes = new();

        public INodeIdentity Identity { get; private set; }
        public ITransport Transport { get; private set; }
        public IDhtStorage Storage { get; private set; }
        public IRoutingTable RoutingTable => _routingTable;

        public DhtNode(
            INodeIdentity identity,
            ITransport transport,
            IDhtStorage storage,
            IRoutingTable routingTable,
            IBootstrapNodeProvider bootstrapNodeProvider,
            IDhtRequestTracker requestTracker,
            IKeyPairService keyPairService,
            IKeyStore keyStore,
            INodeConnectionInfoProvider connectionInfo,
            ILogger<DhtNode> logger,
            ITransportSelector? transportSelector = null,
            IOptions<WebRtcOptions>? webRtcOptions = null)
        {
            Identity = identity;
            Transport = transport;
            Storage = storage;
            _routingTable = routingTable;
            _bootstrapNodeProvider = bootstrapNodeProvider;
            _requestTracker = requestTracker;
            _keypairService = keyPairService;
            _keyStore = keyStore;
            _connectionInfo = connectionInfo;
            _transportSelector = transportSelector ?? new AlwaysTcpTransportSelector(transport);
            _supportsWebRtc = webRtcOptions?.Value.Enabled ?? false;
            _logger = logger;

            _messageHandler = new DhtMessageHandler(
                identity, routingTable, storage, requestTracker,
                SendDhtMessageAsync, () => Transport.Port, logger);

            _lookupService = new DhtLookupService(
                identity, routingTable, storage,
                SendDhtMessageAsync, () => Transport.Port,
                GetBootstrapNodesCopy, logger);
        }

        public void Start(bool enableBootstrap = true, List<RoutingEntry>? overrideBootstrapNodes = null)
        {
            if (_running) return;

            var keys = _keyStore.GetAsync().Result;
            if (keys == null)
                _keypairService.GenerateKeyPairBase64Async().Wait();

            _running = true;

            if (enableBootstrap)
            {
                Task.Run(async () =>
                {
                    List<RoutingEntry> bootstrapNodes;
                    if (overrideBootstrapNodes != null)
                    {
                        bootstrapNodes = overrideBootstrapNodes;
                    }
                    else
                    {
                        var provided = await _bootstrapNodeProvider.GetBootstrapNodesAsync();
                        bootstrapNodes = provided.ToList();
                    }

                    lock (_knownBootstrapNodes)
                    {
                        _knownBootstrapNodes.Clear();
                        _knownBootstrapNodes.AddRange(bootstrapNodes);
                    }

                    await BootstrapAsync(bootstrapNodes);
                });
            }
        }

        public void Stop()
        {
            _running = false;
        }

        public async Task StoreAsync(byte[] contentHash)
        {
            var closestNodes = _routingTable.FindClosest(contentHash, 20);
            foreach (var node in closestNodes)
            {
                var message = new DhtMessage
                {
                    Type = DhtMessageType.Store,
                    SenderNodeId = Identity.NodeId,
                    Payload = contentHash,
                    TimestampUtc = DateTime.UtcNow,
                    Signature = Identity.Sign(contentHash),
                    SenderPort = Transport.Port
                };
                await SendDhtMessageAsync(node.Address, message);
            }
            Storage.StoreContent(contentHash, Identity.NodeId);
        }

        public Task<List<byte[]>> FindValueAsync(byte[] contentHash)
            => _lookupService.FindValueAsync(contentHash);

        public Task<List<ProviderInfo>> FindValueWithAddressAsync(byte[] contentHash)
            => _lookupService.FindValueWithAddressAsync(contentHash);

        public Task<List<RoutingEntry>> FindNodeAsync(byte[] nodeId, RoutingEntry? bootstrap = null)
            => _lookupService.FindNodeAsync(nodeId, bootstrap);

        public async Task PingAsync(RoutingEntry node)
        {
            var message = new DhtMessage
            {
                Type = DhtMessageType.Ping,
                SenderNodeId = Identity.NodeId,
                Payload = Array.Empty<byte>(),
                TimestampUtc = DateTime.UtcNow,
                Signature = Identity.Sign(Array.Empty<byte>()),
                SenderPort = Transport.Port
            };
            await SendDhtMessageAsync(node.Address, message);
        }

        public async Task BootstrapAsync(IEnumerable<RoutingEntry> bootstrapNodes)
        {
            foreach (var bootstrap in bootstrapNodes)
            {
                var randomId = Crypto.RandomNodeId();
                try
                {
                    var foundNodes = await FindNodeAsync(randomId, bootstrap);
                    bool success = false;
                    foreach (var node in foundNodes)
                    {
                        if (node.NodeId?.Length > 0)
                        {
                            _routingTable.AddOrUpdate(new RoutingEntry
                            {
                                NodeId = node.NodeId,
                                Address = node.Address,
                                LastSeenUtc = DateTime.UtcNow
                            });
                            success = true;
                        }
                    }
                    if (success) break;
                }
                catch
                {
                    // ignore unreachable bootstrap nodes
                }
            }
        }

        public Task HandleMessageAsync(DhtMessage message)
            => _messageHandler.HandleAsync(message);

        public async Task<ContentMessage?> SendContentRequestAsync(NodeAddress address, ContentMessage message, TimeSpan timeout)
        {
            message.SenderPort = Transport.Port;

            var tcs = new TaskCompletionSource<ContentMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _requestTracker.RegisterContent(message.RequestId, tcs);

            try
            {
                var json = JsonSerializer.Serialize<ContentMessage>(message);
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                var payload = new byte[1 + jsonBytes.Length];
                payload[0] = (byte)ProtocolKind.Content;
                Array.Copy(jsonBytes, 0, payload, 1, jsonBytes.Length);

                await _transportSelector.SelectFor(address).SendAsync(address, new ReadOnlyMemory<byte>(payload));

                var timeoutTask = Task.Delay(timeout);
                var completed = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completed == tcs.Task)
                    return await tcs.Task;

                _requestTracker.CancelContent(message.RequestId);
                return null;
            }
            catch
            {
                _requestTracker.CancelContent(message.RequestId);
                return null;
            }
        }

        public void HandleContentMessage(ContentMessage message)
        {
            _requestTracker.TryCompleteContent(message.RequestId, message);
        }

        private List<RoutingEntry> GetBootstrapNodesCopy()
        {
            lock (_knownBootstrapNodes)
            {
                return new List<RoutingEntry>(_knownBootstrapNodes);
            }
        }

        private async Task<DhtMessage?> SendDhtMessageAsync(NodeAddress address, DhtMessage message, bool waitForResponse = false)
        {
            try
            {
                message.SenderHttpApiPort = await GetHttpApiPortAsync();

                TaskCompletionSource<DhtMessage>? tcs = null;
                if (waitForResponse)
                {
                    tcs = new TaskCompletionSource<DhtMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _requestTracker.Register(message.RequestId, tcs);
                }

                var json = JsonSerializer.Serialize(message);
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                var payload = new byte[1 + jsonBytes.Length];
                payload[0] = (byte)ProtocolKind.Dht;
                Array.Copy(jsonBytes, 0, payload, 1, jsonBytes.Length);

                message.SupportsWebRtc = _supportsWebRtc;
                await _transportSelector.SelectFor(address).SendAsync(address, new ReadOnlyMemory<byte>(payload));

                if (waitForResponse && tcs != null)
                {
                    var timeoutTask = Task.Delay(2000);
                    var completed = await Task.WhenAny(tcs.Task, timeoutTask);
                    if (completed == tcs.Task)
                        return await tcs.Task;

                    _requestTracker.Cancel(message.RequestId);
                    return null;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send DHT message");
                _requestTracker.Cancel(message.RequestId);
                return null;
            }
        }

        private async Task<int> GetHttpApiPortAsync()
        {
            if (!_httpApiPortResolved)
            {
                var (_, _, port) = await _connectionInfo.GetConnectionInfoAsync();
                _httpApiPort = port;
                _httpApiPortResolved = true;
            }
            return _httpApiPort;
        }
    }

    public class BootstrapNodeConfig
    {
        public string NodeId { get; set; } = string.Empty;
        public BootstrapAddressConfig Address { get; set; } = new();
    }

    public class BootstrapAddressConfig
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
    }

    public class NodeEntry
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public int HttpApiPort { get; set; }
        public bool WebRtcEnabled { get; set; }
    }
}
