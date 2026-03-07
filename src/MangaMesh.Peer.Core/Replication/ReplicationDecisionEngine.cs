using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Node;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Core.Replication;

/// <summary>
/// Decides whether the local node should accept or push a given chunk.
/// Checks three gates: ring responsibility, available storage, and chapter diversity.
/// </summary>
public sealed class ReplicationDecisionEngine : IReplicationDecisionEngine
{
    private readonly IConsistentHashRing _ring;
    private readonly IChapterHealthMonitor _healthMonitor;
    private readonly IChapterDiversityTracker _diversityTracker;
    private readonly IBlobStore _blobStore;
    private readonly INodeIdentity _identity;
    private readonly ReplicationOptions _opts;
    private readonly IReplicationPolicy _policy;

    public ReplicationDecisionEngine(
        IConsistentHashRing ring,
        IChapterHealthMonitor healthMonitor,
        IChapterDiversityTracker diversityTracker,
        IBlobStore blobStore,
        INodeIdentity identity,
        IOptions<ReplicationOptions> options,
        IReplicationPolicy policy)
    {
        _ring = ring;
        _healthMonitor = healthMonitor;
        _diversityTracker = diversityTracker;
        _blobStore = blobStore;
        _identity = identity;
        _opts = options.Value;
        _policy = policy;
    }

    public Task<bool> ShouldAcceptChunkAsync(string blobHash, string chapterId, int totalChunksInChapter = 0, CancellationToken ct = default)
    {
        if (_blobStore.Exists(new Blob.BlobHash(blobHash)))
            return Task.FromResult(false); // already have it

        int k = _policy.GetBaseTargetReplicas();

        // Gate 1: ring responsibility
        bool isResponsible = _ring.IsLocallyResponsible(blobHash, k);
        if (!isResponsible && !_opts.IsSuperSeeder)
            return Task.FromResult(false);

        // Gate 2: diversity constraint — only for super-seeders that bypass ring responsibility.
        // Ring-responsible nodes are already constrained by the ring; applying diversity on top
        // would cause under-replication (20% cap < ~37% ring share at typical node counts).
        if (!isResponsible && _opts.IsSuperSeeder
            && !string.IsNullOrEmpty(chapterId) && totalChunksInChapter > 0
            && !_diversityTracker.CanAcceptChunk(chapterId, totalChunksInChapter))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task<bool> ShouldReplicateChunkAsync(string blobHash, string chapterId, CancellationToken ct = default)
    {
        if (!_blobStore.Exists(new Blob.BlobHash(blobHash)))
            return Task.FromResult(false); // can't push what we don't have

        int k = _policy.GetBaseTargetReplicas();

        // Only the ring leader (first responsible peer) drives replication to avoid storms
        var responsible = _ring.GetResponsiblePeers(blobHash, k);
        bool isLeader = responsible.Count > 0 &&
            responsible[0].NodeId.SequenceEqual(_identity.NodeId);

        if (!isLeader && !_opts.IsSuperSeeder)
            return Task.FromResult(false);

        int currentReplicas = _healthMonitor.EstimateReplicaCount(blobHash, chapterId);
        return Task.FromResult(currentReplicas < k);
    }
}
