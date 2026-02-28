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
        private readonly ILogger<PeerFetcher> _logger;

        public PeerFetcher(
            IPeerLocator peerLocator,
            IBlobStore blobStore,
            IManifestStore manifestStore,
            IDhtNode dhtNode,
            ILogger<PeerFetcher> logger)
        {
            _peerLocator = peerLocator;
            _blobStore = blobStore;
            _manifestStore = manifestStore;
            _dhtNode = dhtNode;
            _logger = logger;
        }

        public async Task<ManifestHash> FetchManifestAsync(string manifestHash)
        {
            var hash = new ManifestHash(manifestHash);

            if (await _manifestStore.ExistsAsync(hash))
            {
                _logger.LogInformation("Manifest {Hash} already available locally", manifestHash);
                return hash;
            }

            _logger.LogInformation("Fetching manifest {Hash} from peers via DHT", manifestHash);

            var providers = await FindProvidersAsync(manifestHash);
            if (providers.Count == 0)
                throw new InvalidOperationException(
                    $"No peers found for manifest {manifestHash}. Ensure at least one peer is online and has announced this manifest.");

            foreach (var address in providers)
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
                    _logger.LogInformation("Stored chapter manifest {Hash}, fetching {PageCount} page blobs", manifestHash, manifest.Files.Count);

                    await FetchAllPageDataAsync(address, manifest);

                    _logger.LogInformation("Successfully fetched and stored manifest {Hash} with all page content", manifestHash);
                    return hash;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch from peer {Host}:{Port}, trying next", address.Host, address.Port);
                }
            }

            throw new InvalidOperationException(
                $"Could not fetch manifest {manifestHash} from any of the {providers.Count} available peer(s).");
        }

        private async Task<List<NodeAddress>> FindProvidersAsync(string manifestHash)
        {
            var addresses = new List<NodeAddress>();

            byte[] hashBytes;
            try { hashBytes = Convert.FromHexString(manifestHash); }
            catch { hashBytes = Encoding.UTF8.GetBytes(manifestHash); }

            // Primary: DHT lookup — finds nodes that stored this manifest hash
            var providers = await _dhtNode.FindValueWithAddressAsync(hashBytes);
            addresses.AddRange(providers.Select(p => p.Address));
            _logger.LogInformation("DHT lookup for {Hash} found {Count} provider(s)", manifestHash, addresses.Count);

            // Fallback: ask tracker for peer NodeIds then resolve via routing table
            if (addresses.Count == 0)
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
                                addresses.Add(address);
                                _logger.LogInformation("Resolved tracker peer {NodeId} to {Host}:{Port}", peer.NodeId, address.Host, address.Port);
                            }
                            else
                            {
                                _logger.LogWarning("Address for NodeId {NodeId} not found in local routing table. Searching DHT for node...", peer.NodeId);
                                var foundNodes = await _dhtNode.FindNodeAsync(nodeIdBytes);
                                var targetEntry = foundNodes.FirstOrDefault(n => n.NodeId != null && n.NodeId.SequenceEqual(nodeIdBytes));
                                if (targetEntry != null && targetEntry.Address != null)
                                {
                                    addresses.Add(targetEntry.Address);
                                    _logger.LogInformation("Actively resolved tracker peer {NodeId} to {Host}:{Port} via DHT FindNode", peer.NodeId, targetEntry.Address.Host, targetEntry.Address.Port);
                                }
                                else
                                {
                                    _logger.LogWarning("Active DHT lookup for NodeId {NodeId} failed to find the peer.", peer.NodeId);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Could not resolve address for NodeId {NodeId}", peer.NodeId);
                        }
                    }

                    _logger.LogInformation("Tracker fallback resolved {Count} peer address(es) for {Hash}", addresses.Count, manifestHash);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while fetching fallback peer list from tracker for {Hash}", manifestHash);
                }
            }

            return addresses;
        }

        private async Task<ChapterManifest?> FetchChapterManifestAsync(NodeAddress address, string manifestHash)
        {
            try
            {
                var request = new GetManifest { ContentHash = manifestHash };
                var response = await _dhtNode.SendContentRequestAsync(address, request, TimeSpan.FromSeconds(30));

                if (response is not ManifestData data)
                {
                    _logger.LogWarning("Peer {Host}:{Port} replied but result was not ManifestData.", address.Host, address.Port);
                    return null;
                }

                var json = Encoding.UTF8.GetString(data.Data);
                return JsonSerializer.Deserialize<ChapterManifest>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FetchChapterManifestAsync failed for {Host}:{Port}", address.Host, address.Port);
                throw;
            }
        }

        private async Task FetchAllPageDataAsync(NodeAddress sourceAddress, ChapterManifest manifest)
        {
            foreach (var file in manifest.Files)
            {
                var pageManifestBlobHash = new BlobHash(file.Hash);
                if (_blobStore.Exists(pageManifestBlobHash))
                {
                    _logger.LogDebug("Page manifest blob {Hash} already present, verifying chunks", file.Hash);
                    await EnsureChunksPresentAsync(sourceAddress, file.Hash);
                    continue;
                }

                var pageManifestData = await FetchBlobAsync(sourceAddress, file.Hash);
                if (pageManifestData == null)
                {
                    _logger.LogWarning("Failed to fetch page manifest blob {Hash}", file.Hash);
                    continue;
                }

                await StoreBlobAsync(pageManifestData);

                try
                {
                    var pageManifest = JsonSerializer.Deserialize<PageManifest>(pageManifestData);
                    if (pageManifest != null)
                        await FetchChunksAsync(sourceAddress, pageManifest.Chunks);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse PageManifest for blob {Hash}", file.Hash);
                }
            }
        }

        private async Task EnsureChunksPresentAsync(NodeAddress sourceAddress, string pageHash)
        {
            try
            {
                await using var stream = await _blobStore.OpenReadAsync(new BlobHash(pageHash));
                if (stream == null) return;

                var pageManifest = await JsonSerializer.DeserializeAsync<PageManifest>(stream);
                if (pageManifest != null)
                    await FetchChunksAsync(sourceAddress, pageManifest.Chunks);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not parse existing page manifest {Hash} to verify chunks", pageHash);
            }
        }

        private async Task FetchChunksAsync(NodeAddress sourceAddress, IReadOnlyList<string> chunkHashes)
        {
            foreach (var chunkHash in chunkHashes)
            {
                if (_blobStore.Exists(new BlobHash(chunkHash))) continue;

                var chunkData = await FetchBlobAsync(sourceAddress, chunkHash);
                if (chunkData != null)
                    await StoreBlobAsync(chunkData);
                else
                    _logger.LogWarning("Failed to fetch image chunk {Hash}", chunkHash);
            }
        }

        private async Task<byte[]?> FetchBlobAsync(NodeAddress address, string blobHash)
        {
            var request = new GetBlob { BlobHash = blobHash };
            var response = await _dhtNode.SendContentRequestAsync(address, request, TimeSpan.FromSeconds(30));
            return response is BlobData data ? data.Data : null;
        }

        private async Task<BlobHash> StoreBlobAsync(byte[] data)
        {
            using var ms = new MemoryStream(data);
            return await _blobStore.PutAsync(ms);
        }
    }
}
