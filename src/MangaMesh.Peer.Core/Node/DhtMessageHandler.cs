using System.Text;
using System.Text.Json;
using MangaMesh.Peer.Core.Transport;
using Microsoft.Extensions.Logging;

namespace MangaMesh.Peer.Core.Node
{
    public class DhtMessageHandler : IDhtMessageHandler
    {
        private readonly INodeIdentity _identity;
        private readonly IRoutingTable _routingTable;
        private readonly IDhtStorage _storage;
        private readonly IDhtRequestTracker _requestTracker;
        private readonly Func<NodeAddress, DhtMessage, bool, Task<DhtMessage?>> _sendMessage;
        private readonly Func<int> _getTransportPort;
        private readonly ILogger _logger;

        public DhtMessageHandler(
            INodeIdentity identity,
            IRoutingTable routingTable,
            IDhtStorage storage,
            IDhtRequestTracker requestTracker,
            Func<NodeAddress, DhtMessage, bool, Task<DhtMessage?>> sendMessage,
            Func<int> getTransportPort,
            ILogger logger)
        {
            _identity = identity;
            _routingTable = routingTable;
            _storage = storage;
            _requestTracker = requestTracker;
            _sendMessage = sendMessage;
            _getTransportPort = getTransportPort;
            _logger = logger;
        }

        public async Task HandleAsync(DhtMessage message)
        {
            if (!string.IsNullOrEmpty(message.ComputedSenderIp) && message.SenderPort > 0)
            {
                _routingTable.AddOrUpdate(new RoutingEntry
                {
                    NodeId = message.SenderNodeId,
                    Address = new NodeAddress(message.ComputedSenderIp, message.SenderPort, HttpApiPort: message.SenderHttpApiPort, WebRtcEnabled: message.SupportsWebRtc),
                    LastSeenUtc = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogDebug("Warning: Received message without valid address info. IP: {IP}, Port: {Port}",
                    message.ComputedSenderIp, message.SenderPort);
            }

            if (_requestTracker.TryComplete(message.RequestId, message))
                return;

            _logger.LogDebug("Received message [{Type}]", message.Type);

            switch (message.Type)
            {
                case DhtMessageType.Ping:
                    await HandlePingAsync(message);
                    break;
                case DhtMessageType.FindNode:
                    await HandleFindNodeAsync(message);
                    break;
                case DhtMessageType.Store:
                    HandleStore(message);
                    break;
                case DhtMessageType.FindValue:
                    await HandleFindValueAsync(message);
                    break;
            }
        }

        private async Task HandlePingAsync(DhtMessage message)
        {
            var pong = new DhtMessage
            {
                Type = DhtMessageType.Pong,
                SenderNodeId = _identity.NodeId,
                Payload = Array.Empty<byte>(),
                TimestampUtc = DateTime.UtcNow,
                Signature = _identity.Sign(Array.Empty<byte>()),
                SenderPort = _getTransportPort(),
                RequestId = message.RequestId
            };

            var senderAddress = ResolveSenderAddress(message);
            if (senderAddress != null)
                await _sendMessage(senderAddress, pong, false);
        }

        private void HandleStore(DhtMessage message)
        {
            _storage.StoreContent(message.Payload, message.SenderNodeId);
        }

        private async Task HandleFindNodeAsync(DhtMessage message)
        {
            var targetId = message.Payload;
            var closestNodes = _routingTable.FindClosest(targetId, 20);
            var nodesPayload = SerializeNodes(closestNodes);

            var reply = new DhtMessage
            {
                Type = DhtMessageType.Nodes,
                SenderNodeId = _identity.NodeId,
                Payload = nodesPayload,
                TimestampUtc = DateTime.UtcNow,
                Signature = _identity.Sign(nodesPayload),
                SenderPort = _getTransportPort(),
                RequestId = message.RequestId
            };

            var senderAddress = ResolveSenderAddress(message);
            if (senderAddress != null)
                await _sendMessage(senderAddress, reply, false);
        }

        private async Task HandleFindValueAsync(DhtMessage message)
        {
            var contentHash = message.Payload;
            var nodesWithContent = _storage.GetNodesForContent(contentHash);

            DhtMessage reply;
            if (nodesWithContent.Count > 0)
            {
                // Include full address info so the requester can connect without a separate routing table lookup.
                // If the provider is not in our routing table (e.g. this node is the provider),
                // the requester falls back to the sender IP/port from the message envelope.
                var providerEntries = nodesWithContent.Select(nodeId =>
                {
                    var address = _routingTable.GetAddressForNode(nodeId);
                    return new NodeEntry
                    {
                        NodeId = Convert.ToHexString(nodeId),
                        Host = address?.Host ?? string.Empty,
                        Port = address?.Port ?? 0,
                        HttpApiPort = address?.HttpApiPort ?? 0,
                        WebRtcEnabled = address?.WebRtcEnabled ?? false
                    };
                }).ToList();

                var payload = JsonSerializer.SerializeToUtf8Bytes(providerEntries);
                reply = new DhtMessage
                {
                    Type = DhtMessageType.Value,
                    SenderNodeId = _identity.NodeId,
                    Payload = payload,
                    TimestampUtc = DateTime.UtcNow,
                    Signature = _identity.Sign(payload),
                    SenderPort = _getTransportPort(),
                    RequestId = message.RequestId
                };
            }
            else
            {
                var closestNodes = _routingTable.FindClosest(contentHash, 20);
                var payload = SerializeNodes(closestNodes);
                reply = new DhtMessage
                {
                    Type = DhtMessageType.Nodes,
                    SenderNodeId = _identity.NodeId,
                    Payload = payload,
                    TimestampUtc = DateTime.UtcNow,
                    Signature = _identity.Sign(payload),
                    SenderPort = _getTransportPort(),
                    RequestId = message.RequestId
                };
            }

            var senderAddress = ResolveSenderAddress(message);
            if (senderAddress != null)
                await _sendMessage(senderAddress, reply, false);
        }

        private NodeAddress? ResolveSenderAddress(DhtMessage message)
        {
            var address = _routingTable.GetAddressForNode(message.SenderNodeId);
            if (address == null && !string.IsNullOrEmpty(message.ComputedSenderIp) && message.SenderPort > 0)
                address = new NodeAddress(message.ComputedSenderIp, message.SenderPort);
            return address;
        }

        private static byte[] SerializeNodes(IReadOnlyList<RoutingEntry> nodes)
        {
            var addresses = nodes.Select(n => new
            {
                Host = n.Address.Host,
                Port = n.Address.Port,
                NodeId = Convert.ToHexString(n.NodeId),
                HttpApiPort = n.Address.HttpApiPort,
                WebRtcEnabled = n.Address.WebRtcEnabled
            }).ToList();
            return JsonSerializer.SerializeToUtf8Bytes(addresses);
        }
    }
}
