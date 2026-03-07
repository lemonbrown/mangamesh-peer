using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Replication;
using MangaMesh.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MangaMesh.Peer.ClientApi.Controllers;

[ApiController]
[Route("api/replication")]
[Authorize]
public sealed class ReplicationController : ControllerBase
{
    private readonly IChapterHealthMonitor _healthMonitor;
    private readonly IPeerStorageProfileProvider _profileProvider;
    private readonly IReplicationPolicy _policy;
    private readonly IConsistentHashRing _ring;
    private readonly IManifestStore _manifestStore;
    private readonly INodeIdentity _identity;
    private readonly IServiceScopeFactory _scopeFactory;

    public ReplicationController(
        IChapterHealthMonitor healthMonitor,
        IPeerStorageProfileProvider profileProvider,
        IReplicationPolicy policy,
        IConsistentHashRing ring,
        IManifestStore manifestStore,
        INodeIdentity identity,
        IServiceScopeFactory scopeFactory)
    {
        _healthMonitor = healthMonitor;
        _profileProvider = profileProvider;
        _policy = policy;
        _ring = ring;
        _manifestStore = manifestStore;
        _identity = identity;
        _scopeFactory = scopeFactory;
    }

    /// <summary>Returns the local node's current storage profile.</summary>
    [HttpGet("profile")]
    public IActionResult GetProfile()
    {
        return Ok(_profileProvider.GetLocalProfile());
    }

    /// <summary>Returns all known chapter health states.</summary>
    [HttpGet("health")]
    public IActionResult GetAllHealth()
    {
        return Ok(_healthMonitor.GetAllHealthStates());
    }

    /// <summary>Returns health state for a specific chapter manifest.</summary>
    [HttpGet("health/{manifestHash}")]
    public async Task<IActionResult> GetChapterHealth(string manifestHash)
    {
        var manifest = await _manifestStore.GetAsync(new ManifestHash(manifestHash));
        if (manifest is null)
            return NotFound();

        var target = _policy.GetTarget(manifest);

        // Collect all chunk hashes from page manifests that exist locally
        var chunkHashes = manifest.Files
            .Select(f => f.Hash)
            .ToList();

        var state = _healthMonitor.GetHealthState(
            manifest.ChapterId,
            manifestHash,
            chunkHashes,
            target.MinimumReplicas);

        return Ok(new
        {
            State = state,
            Target = target
        });
    }

    /// <summary>Triggers a repair pass for a single chapter.</summary>
    [HttpPost("repair/{manifestHash}")]
    public async Task<IActionResult> RepairChapter(string manifestHash, CancellationToken ct)
    {
        var manifest = await _manifestStore.GetAsync(new ManifestHash(manifestHash));
        if (manifest is null)
            return NotFound();

        using var scope = _scopeFactory.CreateScope();
        var scheduler = scope.ServiceProvider.GetService<IRepairScheduler>();
        if (scheduler is null)
            return StatusCode(503, "Replication service is not enabled");

        //await scheduler.RepairChapterAsync(manifestHash, ct);
        return Accepted();
    }

    /// <summary>
    /// Returns the ring-responsible peers for a chunk hash (debug / diagnostics).
    /// </summary>
    [HttpGet("ring/{chunkHash}")]
    public IActionResult GetRingPeers(string chunkHash, [FromQuery] int replicas = 12)
    {
        var peers = _ring.GetResponsiblePeers(chunkHash, replicas);
        bool isLocal = _ring.IsLocallyResponsible(chunkHash, replicas);

        return Ok(new
        {
            ChunkHash = chunkHash,
            TargetReplicas = replicas,
            IsLocallyResponsible = isLocal,
            Peers = peers.Select(p => new
            {
                NodeId = Convert.ToHexString(p.NodeId).ToLowerInvariant(),
                Host = p.Address.Host,
                Port = p.Address.Port,
                FreeStorageMb = p.FreeStorageBytes / (1024.0 * 1024.0),
                UptimeScore = p.UptimeScore
            })
        });
    }

    /// <summary>
    /// Returns a per-series replication overview: local chunk coverage and replica estimates
    /// for every manifest held by this node.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromServices] IBlobStore blobStore)
    {
        var allManifests = await _manifestStore.GetAllWithDataAsync();

        var localBlobs = new HashSet<string>(
            blobStore.GetAllHashes().Select(h => h.Value),
            StringComparer.OrdinalIgnoreCase);

        var seriesOut = new List<OverviewSeriesDto>();

        foreach (var g in allManifests.GroupBy(m => m.Manifest.SeriesId))
        {
            var seriesTitle = g.Select(x => x.Manifest.SeriesTitle).FirstOrDefault(t => !string.IsNullOrEmpty(t));
            var externalMangaId = g.Select(x => x.Manifest.ExternalMangaId).FirstOrDefault(id => !string.IsNullOrEmpty(id));

            var chapters = new List<OverviewChapterDto>();

            foreach (var x in g)
            {
                var files = (IReadOnlyList<ChapterFileEntry>)(x.Manifest.Files
                    ?? (IReadOnlyList<ChapterFileEntry>)Array.Empty<ChapterFileEntry>());
                var localBytes = files.Where(f => localBlobs.Contains(f.Hash)).Sum(f => f.Size);
                var target = _policy.GetTarget(x.Manifest);

                // Resolve image chunk hashes from page manifests so that
                // EstimateReplicaCount operates at the correct (chunk data) level.
                var chunkHashes = new List<string>();
                foreach (var file in files)
                {
                    using var stream = await blobStore.OpenReadAsync(new BlobHash(file.Hash));
                    if (stream is null) continue;
                    try
                    {
                        var pm = await JsonSerializer.DeserializeAsync<PageManifest>(stream,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (pm?.Chunks is not null)
                            chunkHashes.AddRange(pm.Chunks);
                    }
                    catch { /* corrupt page manifest — skip */ }
                }

                // Fall back to page-manifest hashes when page manifests aren't locally held
                if (chunkHashes.Count == 0)
                    chunkHashes.AddRange(files.Select(f => f.Hash));

                // Re-register local ownership so replica counts are correct after restarts.
                // GossipChapterHealthMonitor is in-memory; this re-seeds it from the blob store.
                string localPeerId = Convert.ToHexString(_identity.NodeId).ToLowerInvariant();
                foreach (string h in chunkHashes)
                {
                    if (localBlobs.Contains(h))
                        _healthMonitor.RecordChunkOwner(h, localPeerId);
                }

                var health = _healthMonitor.GetHealthState(
                    x.Manifest.ChapterId, x.Hash.Value, chunkHashes, target.MinimumReplicas);

                chapters.Add(new OverviewChapterDto
                {
                    ManifestHash = x.Hash.Value,
                    ChapterNumber = x.Manifest.ChapterNumber,
                    Title = x.Manifest.Title,
                    Language = x.Manifest.Language,
                    ScanGroup = x.Manifest.ScanGroup,
                    IsDownloaded = x.IsDownloaded,
                    TotalBytes = x.Manifest.TotalSize,
                    LocalBytes = localBytes,
                    TotalChunks = chunkHashes.Count > 0 ? chunkHashes.Count : files.Count,
                    LocalChunks = chunkHashes.Count(h => localBlobs.Contains(h)),
                    ReplicaEstimate = health.ReplicaEstimate,
                    RareChunkCount = health.RareChunkCount,
                    TargetReplicas = target.TargetReplicas,
                    MinimumReplicas = target.MinimumReplicas,
                });
            }

            chapters.Sort((a, b) => a.ChapterNumber.CompareTo(b.ChapterNumber));

            seriesOut.Add(new OverviewSeriesDto
            {
                SeriesId = g.Key,
                SeriesTitle = seriesTitle,
                ExternalMangaId = externalMangaId,
                ReplicaEstimate = chapters.Count > 0 ? chapters.Min(c => c.ReplicaEstimate) : 0,
                Chapters = chapters,
            });
        }

        seriesOut.Sort((a, b) => string.Compare(
            a.SeriesTitle ?? a.SeriesId, b.SeriesTitle ?? b.SeriesId,
            StringComparison.OrdinalIgnoreCase));

        return Ok(new { Series = seriesOut });
    }

    private sealed class OverviewChapterDto
    {
        public string ManifestHash { get; init; } = "";
        public double ChapterNumber { get; init; }
        public string? Title { get; init; }
        public string? Language { get; init; }
        public string? ScanGroup { get; init; }
        public bool IsDownloaded { get; init; }
        public long TotalBytes { get; init; }
        public long LocalBytes { get; init; }
        public int TotalChunks { get; init; }
        public int LocalChunks { get; init; }
        public int ReplicaEstimate { get; init; }
        public int RareChunkCount { get; init; }
        public int TargetReplicas { get; init; }
        public int MinimumReplicas { get; init; }
    }

    private sealed class OverviewSeriesDto
    {
        public string SeriesId { get; init; } = "";
        public string? SeriesTitle { get; init; }
        public string? ExternalMangaId { get; init; }
        public int ReplicaEstimate { get; init; }
        public List<OverviewChapterDto> Chapters { get; init; } = [];
    }
}
