using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MangaMesh.Peer.Core.Replication;

/// <summary>
/// Scans locally stored manifests and pushes under-replicated chunks to ring-assigned peers.
/// </summary>
public sealed class RepairScheduler : IRepairScheduler
{
    private readonly IManifestStore _manifestStore;
    private readonly IBlobStore _blobStore;
    private readonly IReplicationPolicy _policy;
    private readonly IChapterHealthMonitor _healthMonitor;
    private readonly IReplicationExecutor _executor;
    private readonly IPeerScorer _scorer;
    private readonly ILogger<RepairScheduler> _logger;

    public RepairScheduler(
        IManifestStore manifestStore,
        IBlobStore blobStore,
        IReplicationPolicy policy,
        IChapterHealthMonitor healthMonitor,
        IReplicationExecutor executor,
        IPeerScorer scorer,
        ILogger<RepairScheduler> logger)
    {
        _manifestStore = manifestStore;
        _blobStore = blobStore;
        _policy = policy;
        _healthMonitor = healthMonitor;
        _executor = executor;
        _scorer = scorer;
        _logger = logger;
    }

    public async Task ScanAndRepairAsync(CancellationToken ct = default)
    {
        IEnumerable<ManifestHash> hashes;
        try
        {
            hashes = await _manifestStore.GetAllHashesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate manifests during repair scan");
            return;
        }

        foreach (ManifestHash hash in hashes)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await RepairChapterAsync(hash.Value, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Repair failed for manifest {Hash}", hash.Value);
            }
        }
    }

    public async Task RepairChapterAsync(string manifestHash, CancellationToken ct = default)
    {
        ChapterManifest? manifest = await _manifestStore.GetAsync(new ManifestHash(manifestHash));
        if (manifest is null)
            return;

        ChunkReplicaTarget target = _policy.GetTarget(manifest);

        foreach (ChapterFileEntry file in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();

            // Resolve page manifest to get chunk hashes
            if (!_blobStore.Exists(new BlobHash(file.Hash)))
                continue;

            PageManifest? pageManifest = await ReadPageManifestAsync(file.Hash);
            if (pageManifest is null)
                continue;

            foreach (string chunkHash in pageManifest.Chunks)
            {
                ct.ThrowIfCancellationRequested();
                int replicas = _healthMonitor.EstimateReplicaCount(chunkHash);

                if (replicas >= target.TargetReplicas)
                    continue;

                int priority = replicas < target.MinimumReplicas ? 2 : 0;

                _logger.LogDebug(
                    "Chunk {Hash} has {Replicas}/{Target} replicas — repairing (priority={P})",
                    chunkHash[..8], replicas, target.TargetReplicas, priority);

                await _executor.PushToRingPeersAsync(chunkHash, manifest.ChapterId, target.TargetReplicas, ct);
            }
        }
    }

    private async Task<PageManifest?> ReadPageManifestAsync(string pageManifestHash)
    {
        try
        {
            using Stream? stream = await _blobStore.OpenReadAsync(new BlobHash(pageManifestHash));
            if (stream is null)
                return null;

            return await JsonSerializer.DeserializeAsync<PageManifest>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }
}
