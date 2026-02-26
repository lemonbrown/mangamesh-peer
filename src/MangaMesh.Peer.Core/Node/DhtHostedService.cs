using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Node
{
    public class DhtHostedService : IHostedService
    {
        private readonly IDhtNode _dhtNode;
        private readonly IDhtMaintenanceService _maintenanceService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DhtHostedService> _logger;

        public DhtHostedService(
            IDhtNode dhtNode,
            IDhtMaintenanceService maintenanceService,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<DhtHostedService> logger)
        {
            _dhtNode = dhtNode;
            _maintenanceService = maintenanceService;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting DhtHostedService...");
            var enableBootstrap = _configuration.GetValue<bool>("Dht:Bootstrap", true);
            var bootstrapNodesConfig = _configuration.GetValue<string>("Dht:BootstrapNodes");
            var bootstrapNodes = new List<RoutingEntry>();

            if (!string.IsNullOrEmpty(bootstrapNodesConfig))
            {
                var nodes = bootstrapNodesConfig.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var node in nodes)
                {
                    var parts = node.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var port))
                    {
                        bootstrapNodes.Add(new RoutingEntry
                        {
                            NodeId = Array.Empty<byte>(),
                            Address = new NodeAddress(parts[0], port),
                            LastSeenUtc = DateTime.UtcNow
                        });
                    }
                }
                _logger.LogInformation("Parsed {Count} bootstrap nodes from config.", bootstrapNodes.Count);
            }

            _dhtNode.Start(enableBootstrap, bootstrapNodes.Count > 0 ? bootstrapNodes : null);
            _maintenanceService.Start();

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                using var scope = _scopeFactory.CreateScope();

                // Re-announce blob hashes from persistent storage
                var blobStore = scope.ServiceProvider.GetRequiredService<IBlobStore>();
                var blobHashes = blobStore.GetAllHashes().ToList();
                _logger.LogInformation("Re-announcing {Count} on-disk blobs to DHT after startup.", blobHashes.Count);
                foreach (var blobHash in blobHashes)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    try { await _dhtNode.StoreAsync(Convert.FromHexString(blobHash.Value)); }
                    catch { /* best-effort */ }
                }

                // Re-announce manifest hashes from persistent storage.
                // The gateway discovers peers by manifest hash, so these must be in the DHT.
                // Without this, manifests imported before a restart are invisible to the gateway.
                var manifestStore = scope.ServiceProvider.GetService<IManifestStore>();
                if (manifestStore != null)
                {
                    var manifestHashes = (await manifestStore.GetAllHashesAsync()).ToList();
                    _logger.LogInformation("Re-announcing {Count} manifest hashes to DHT after startup.", manifestHashes.Count);
                    foreach (var manifestHash in manifestHashes)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        try { await _dhtNode.StoreAsync(Convert.FromHexString(manifestHash.Value)); }
                        catch { /* best-effort */ }
                    }
                }

                _logger.LogInformation("DHT startup re-announcement complete.");
            }, cancellationToken);

            _logger.LogInformation("DhtHostedService started.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping DhtHostedService...");
            _maintenanceService.Stop();
            _dhtNode.Stop();
            return Task.CompletedTask;
        }
    }
}
