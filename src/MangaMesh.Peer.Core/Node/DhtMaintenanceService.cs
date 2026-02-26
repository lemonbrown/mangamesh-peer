using System.Net.Http;
using System.Net.Sockets;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MangaMesh.Peer.Core.Node
{
    public class DhtMaintenanceService : IDhtMaintenanceService
    {
        private readonly TimeSpan _reannounceInterval = TimeSpan.FromMinutes(30);
        private readonly TimeSpan _pingInterval = TimeSpan.FromMinutes(5);

        private CancellationTokenSource? _maintenanceToken;

        private readonly IDhtNode _dhtNode;
        private readonly IRoutingTable _routingTable;
        private readonly IDhtStorage _storage;
        private readonly IManifestStore? _manifestStore;
        private readonly INodeAnnouncer _tracker;
        private readonly INodeIdentity _identity;
        private readonly INodeIdentityService? _identityService;
        private readonly ILogger<DhtMaintenanceService> _logger;

        // Tracks which manifest hashes have been announced to DHT in the current cycle.
        // Cleared on every 30-minute re-announcement so all hashes are re-verified.
        private readonly HashSet<string> _dhtAnnouncedHashes = new();

        public DhtMaintenanceService(
            IDhtNode dhtNode,
            IRoutingTable routingTable,
            IDhtStorage storage,
            INodeAnnouncer tracker,
            INodeIdentity identity,
            ILogger<DhtMaintenanceService> logger,
            IManifestStore? manifestStore = null,
            INodeIdentityService? identityService = null)
        {
            _dhtNode = dhtNode;
            _routingTable = routingTable;
            _storage = storage;
            _tracker = tracker;
            _identity = identity;
            _manifestStore = manifestStore;
            _identityService = identityService;
            _logger = logger;
        }

        public void Start()
        {
            _maintenanceToken = new CancellationTokenSource();
            Task.Run(() => MaintenanceLoopAsync(_maintenanceToken.Token));
        }

        public void Stop()
        {
            _maintenanceToken?.Cancel();
        }

        private async Task MaintenanceLoopAsync(CancellationToken token)
        {
            var lastReannounce = DateTime.UtcNow;
            var lastPing = DateTime.UtcNow;

            while (!token.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;

                if (now - lastReannounce > _reannounceInterval)
                {
                    foreach (var content in _storage.GetAllContentHashes())
                        await _dhtNode.StoreAsync(content);
                    // Reset so all hashes get re-verified on the next iteration
                    _dhtAnnouncedHashes.Clear();
                    lastReannounce = now;
                }

                // Announce any newly imported manifest hashes to DHT immediately (within 10s of import).
                // This ensures chapters imported while the node is running are discoverable
                // even if the StoreAsync call in ChapterPublisherService failed transiently.
                await AnnounceNewManifestHashesToDhtAsync();

                if (now - lastPing > _pingInterval)
                {
                    foreach (var entry in _routingTable.GetAll())
                    {
                        if ((now - entry.LastSeenUtc) > _pingInterval)
                            await _dhtNode.PingAsync(entry);
                    }
                    lastPing = now;
                }

                await AnnounceToIndexAsync();
                await Task.Delay(TimeSpan.FromSeconds(10), token);
            }
        }

        private async Task AnnounceNewManifestHashesToDhtAsync()
        {
            if (_manifestStore == null) return;
            try
            {
                var allHashes = await _manifestStore.GetAllHashesAsync();
                foreach (var hash in allHashes)
                {
                    if (_dhtAnnouncedHashes.Contains(hash.Value)) continue;
                    try
                    {
                        await _dhtNode.StoreAsync(Convert.FromHexString(hash.Value));
                        _dhtAnnouncedHashes.Add(hash.Value);
                        _logger.LogDebug("Announced manifest hash {Hash} to DHT.", hash.Value[..8]);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to announce manifest hash {Hash} to DHT.", hash.Value[..8]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check for new manifest hashes to announce to DHT.");
            }
        }

        private static bool IsConnectivityFailure(HttpRequestException ex) =>
            ex.InnerException is SocketException or IOException
            || ex.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("Connection reset", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("No such host", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase);

        private async Task AnnounceToIndexAsync()
        {
            try
            {
                _logger.LogDebug("DhtMaintenanceService: Announcing to index...");

                List<string> manifests;
                List<ManifestSummary>? manifestData = null;

                if (_manifestStore != null)
                {
                    var allManifests = await _manifestStore.GetAllWithDataAsync();
                    manifests = allManifests.Select(x => x.Hash.Value).ToList();
                    manifestData = allManifests.Select(x => new ManifestSummary
                    {
                        Hash = x.Hash.Value,
                        SeriesId = x.Manifest.SeriesId,
                        ChapterId = x.Manifest.ChapterId,
                        Title = x.Manifest.Title,
                        ChapterNumber = x.Manifest.ChapterNumber,
                        Volume = x.Manifest.Volume,
                        Language = x.Manifest.Language,
                        ScanGroup = x.Manifest.ScanGroup,
                        Quality = x.Manifest.Quality,
                        TotalSize = x.Manifest.TotalSize,
                        CreatedUtc = x.Manifest.CreatedUtc
                    }).ToList();
                }
                else
                {
                    manifests = _storage.GetAllContentHashes()
                        .Select(h => Convert.ToHexString(h).ToLowerInvariant())
                        .ToList();
                }

                _logger.LogDebug("DhtMaintenanceService: Found {Count} manifests to announce.", manifests.Count);

                var request = new Shared.Models.AnnounceRequest(
                    Convert.ToHexString(_identity.NodeId).ToLowerInvariant(),
                    manifests,
                    manifestData);

                await _tracker.AnnounceAsync(request);
                _logger.LogDebug("DhtMaintenanceService: Announcement successful.");
                _identityService?.UpdateStatus(true);
            }
            catch (HttpRequestException ex) when (IsConnectivityFailure(ex))
            {
                _logger.LogDebug("Tracker unreachable: {Message}", ex.InnerException?.Message ?? ex.Message);
                _identityService?.UpdateStatus(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to announce to index");
                _identityService?.UpdateStatus(false);
            }
        }
    }
}
