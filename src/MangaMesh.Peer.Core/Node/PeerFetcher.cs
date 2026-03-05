using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace MangaMesh.Peer.Core.Node
{
    public sealed class PeerFetcher : IPeerFetcher
    {
        private readonly IPeerLocator _peerLocator;
        private readonly IBlobStore _blobStore;
        private readonly IManifestStore _manifestStore;
        private readonly IDhtNode _dhtNode;
        private readonly ISourceProviderCache _providerCache;
        private readonly ILogger<PeerFetcher> _logger;

        public PeerFetcher(
            IPeerLocator peerLocator,
            IBlobStore blobStore,
            IManifestStore manifestStore,
            IDhtNode dhtNode,
            ISourceProviderCache providerCache,
            ILogger<PeerFetcher> logger)
        {
            _peerLocator = peerLocator;
            _blobStore = blobStore;
            _manifestStore = manifestStore;
            _dhtNode = dhtNode;
            _providerCache = providerCache;
            _logger = logger;
        }

        public async Task<(ManifestHash Hash, string? DeliveredByNodeId)> FetchManifestAsync(string manifestHash)
        {
            var hash = new ManifestHash(manifestHash);

            if (await _manifestStore.ExistsAsync(hash))
            {
                _logger.LogInformation("Manifest {Hash} already available locally", manifestHash);
                return (hash, null);
            }

            _logger.LogInformation("Fetching manifest {Hash} from peers via DHT", manifestHash);

            var providers = await FindProvidersAsync(manifestHash);
            if (providers.Count == 0)
                throw new InvalidOperationException(
                    $"No peers found for manifest {manifestHash}. Ensure at least one peer is online and has announced this manifest.");

            foreach (var (address, nodeId) in providers)
            {
                try
                {
                    _logger.LogInformation("Trying peer {Host}:{Port}", address.Host, address.Port);

                    var manifest = await FetchChapterManifestAsync(address, manifestHash);
                    if (manifest == null)
                    {
                        _logger.LogWarning("Peer {Host}:{Port} did not return manifest {Hash}", address.Host, address.Port, manifestHash);
                        continue;
                    }

                    await _manifestStore.SaveAsync(hash, manifest);

                    // Register source address for every page blob hash so BlobController can
                    // proxy-fetch them on demand without storing any blob data locally.
                    _providerCache.RegisterSources(manifest.Files.Select(f => f.Hash), address);

                    _logger.LogInformation(
                        "Stored manifest {Hash} ({PageCount} pages) from peer {NodeId}; page blobs registered for proxy",
                        manifestHash, manifest.Files.Count, nodeId ?? "unknown");

                    return (hash, nodeId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch from peer {Host}:{Port}, trying next", address.Host, address.Port);
                }
            }

            throw new InvalidOperationException(
                $"Could not fetch manifest {manifestHash} from any of the {providers.Count} available peer(s).");
        }

        public async Task<byte[]?> FetchBlobForProxyAsync(
            NodeAddress source, string blobHash, CancellationToken ct = default)
        {
            try
            {
                var request = new GetBlob { BlobHash = blobHash };
                var response = await _dhtNode.SendContentRequestAsync(source, request, TimeSpan.FromSeconds(30));
                return response is BlobData data ? data.Data : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Proxy fetch of blob {Hash} from {Host}:{Port} failed",
                    blobHash, source.Host, source.Port);
                return null;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private async Task<List<(NodeAddress Address, string? NodeId)>> FindProvidersAsync(string manifestHash)
        {
            var providers = new List<(NodeAddress Address, string? NodeId)>();

            byte[] hashBytes;
            try { hashBytes = Convert.FromHexString(manifestHash); }
            catch { hashBytes = Encoding.UTF8.GetBytes(manifestHash); }

            var dhtProviders = await _dhtNode.FindValueWithAddressAsync(hashBytes);
            providers.AddRange(dhtProviders.Select(p => (
                p.Address,
                p.NodeId is { Length: > 0 } ? Convert.ToHexString(p.NodeId).ToLower() : (string?)null
            )));
            _logger.LogInformation("DHT lookup for {Hash} found {Count} provider(s)", manifestHash, providers.Count);

            if (providers.Count == 0)
            {
                _logger.LogInformation("DHT found no providers; falling back to tracker peer list for {Hash}", manifestHash);
                try
                {
                    var peers = await _peerLocator.GetPeersForManifestAsync(manifestHash);
                    _logger.LogInformation("Tracker returned {Count} peer(s) for {Hash}", peers.Count, manifestHash);

                    foreach (var peer in peers)
                    {
                        try
                        {
                            var nodeIdBytes = Convert.FromHexString(peer.NodeId);
                            var address = _dhtNode.RoutingTable.GetAddressForNode(nodeIdBytes);
                            if (address != null)
                            {
                                providers.Add((address, peer.NodeId));
                                _logger.LogInformation("Resolved tracker peer {NodeId} to {Host}:{Port}", peer.NodeId, address.Host, address.Port);
                            }
                            else
                            {
                                _logger.LogWarning("Address for NodeId {NodeId} not found in routing table, searching DHT", peer.NodeId);
                                var foundNodes = await _dhtNode.FindNodeAsync(nodeIdBytes);
                                var target = foundNodes.FirstOrDefault(n => n.NodeId?.SequenceEqual(nodeIdBytes) == true);
                                if (target?.Address != null)
                                {
                                    providers.Add((target.Address, peer.NodeId));
                                    _logger.LogInformation("Resolved tracker peer {NodeId} via DHT FindNode", peer.NodeId);
                                }
                                else
                                {
                                    _logger.LogWarning("DHT FindNode for {NodeId} found nothing", peer.NodeId);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Could not resolve address for NodeId {NodeId}", peer.NodeId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception fetching fallback peer list from tracker for {Hash}", manifestHash);
                }
            }

            return providers;
        }

        private async Task<ChapterManifest?> FetchChapterManifestAsync(NodeAddress address, string manifestHash)
        {
            var request = new GetManifest { ContentHash = manifestHash };
            var response = await _dhtNode.SendContentRequestAsync(address, request, TimeSpan.FromSeconds(30));

            if (response is not ManifestData data)
            {
                _logger.LogWarning("Peer {Host}:{Port} replied but result was not ManifestData", address.Host, address.Port);
                return null;
            }

            var json = Encoding.UTF8.GetString(data.Data);
            return JsonSerializer.Deserialize<ChapterManifest>(json);
        }
    }
}
