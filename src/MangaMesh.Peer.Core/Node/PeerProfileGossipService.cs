using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Replication;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Core.Node;

/// <summary>
/// Periodically broadcasts the local peer's storage profile to a sample of routing
/// table neighbours via <see cref="ChapterHealthGossip"/>.
/// Storage profile fields are also piggy-backed onto every outgoing <see cref="Transport.DhtMessage"/>
/// via <see cref="DhtNode"/> — this service handles the active gossip push.
/// </summary>
public sealed class PeerProfileGossipService : BackgroundService
{
    private readonly IDhtNode _dhtNode;
    private readonly IPeerStorageProfileProvider _profileProvider;
    private readonly IChapterHealthMonitor _healthMonitor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MangaMesh.Peer.Core.Node.INodeIdentity _identity;
    private readonly ReplicationOptions _opts;
    private readonly ILogger<PeerProfileGossipService> _logger;

    public PeerProfileGossipService(
        IDhtNode dhtNode,
        IPeerStorageProfileProvider profileProvider,
        IChapterHealthMonitor healthMonitor,
        IServiceScopeFactory scopeFactory,
        MangaMesh.Peer.Core.Node.INodeIdentity identity,
        IOptions<ReplicationOptions> options,
        ILogger<PeerProfileGossipService> logger)
    {
        _dhtNode = dhtNode;
        _profileProvider = profileProvider;
        _healthMonitor = healthMonitor;
        _scopeFactory = scopeFactory;
        _identity = identity;
        _opts = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.Enabled)
            return;

        _logger.LogInformation("Peer profile gossip service started (interval={Interval}s)", _opts.GossipIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_opts.GossipIntervalSeconds), stoppingToken);
                await GossipAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gossip cycle failed");
            }
        }
    }

    private async Task GossipAsync(CancellationToken ct)
    {
        PeerStorageProfile profile = _profileProvider.GetLocalProfile();
        IReadOnlyList<ChapterHealthState> healthStates = _healthMonitor.GetAllHealthStates();

        if (healthStates.Count == 0)
            return;

        // Sample up to 5 random neighbours from routing table
        var neighbours = _dhtNode.RoutingTable.GetAll()
            .OrderBy(_ => Guid.NewGuid())
            .Take(5)
            .ToList();

        if (neighbours.Count == 0)
            return;

        var topItems = healthStates
            .OrderByDescending(s => s.RareChunkCount)
            .Take(20)
            .ToList();

        var filters = new Dictionary<string, byte[]>();

        using var scope = _scopeFactory.CreateScope();
        var blobStore = scope.ServiceProvider.GetRequiredService<MangaMesh.Peer.Core.Blob.IBlobStore>();
        var manifestStore = scope.ServiceProvider.GetRequiredService<MangaMesh.Peer.Core.Manifests.IManifestStore>();

        var localBlobs = new HashSet<string>(
            blobStore.GetAllHashes().Select(h => h.Value),
            StringComparer.OrdinalIgnoreCase);

        foreach (var state in topItems)
        {
            var manifest = await manifestStore.GetAsync(new ManifestHash(state.ManifestHash));
            if (manifest == null) continue;

            var files = (IReadOnlyList<MangaMesh.Shared.Models.ChapterFileEntry>)(manifest.Files
                ?? (IReadOnlyList<MangaMesh.Shared.Models.ChapterFileEntry>)Array.Empty<MangaMesh.Shared.Models.ChapterFileEntry>());

            int totalChunks = state.TotalChunkCount > 0 ? state.TotalChunkCount : files.Count;
            if (totalChunks == 0) continue;

            var filter = new MangaMesh.Peer.Core.Replication.BloomFilter(totalChunks, 0.05);
            bool addedAny = false;

            foreach (var f in files)
            {
                // we scan the local page manifestations to identify chunk ownership
                using var stream = await blobStore.OpenReadAsync(new MangaMesh.Peer.Core.Blob.BlobHash(f.Hash));
                if (stream != null)
                {
                    try
                    {
                        var pm = await System.Text.Json.JsonSerializer.DeserializeAsync<MangaMesh.Shared.Models.PageManifest>(stream,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (pm?.Chunks != null)
                        {
                            foreach (var chunkHash in pm.Chunks)
                            {
                                if (localBlobs.Contains(chunkHash))
                                {
                                    filter.Add(chunkHash);
                                    addedAny = true;
                                }
                            }
                        }
                    }
                    catch { /* corrupt PM */ }
                }
                else if (localBlobs.Contains(f.Hash))
                {
                    filter.Add(f.Hash);
                    addedAny = true;
                }
            }

            if (addedAny)
            {
                filters[state.ChapterId] = filter.ToByteArray();
            }
        }

        var gossipMsg = new ChapterHealthGossip
        {
            SenderPeerId = Convert.ToHexString(_identity.NodeId).ToLowerInvariant(),
            Items = topItems,
            ChunkBloomFilters = filters
        };

        _logger.LogDebug("Gossiping {Count} chapter health states and {FiltersCount} filters to {Peers} peers",
            gossipMsg.Items.Count, filters.Count, neighbours.Count);

        foreach (var neighbour in neighbours)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _dhtNode.SendContentRequestAsync(neighbour.Address, gossipMsg, TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Gossip to {Host} failed", neighbour.Address.Host);
            }
        }
    }
}
