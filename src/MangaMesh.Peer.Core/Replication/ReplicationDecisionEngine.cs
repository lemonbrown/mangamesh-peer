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

    public ReplicationDecisionEngine(
        IConsistentHashRing ring,
        IChapterHealthMonitor healthMonitor,
        IChapterDiversityTracker diversityTracker,
        IBlobStore blobStore,
        INodeIdentity identity,
        IOptions<ReplicationOptions> options)
    {
        _ring = ring;
        _healthMonitor = healthMonitor;
        _diversityTracker = diversityTracker;
        _blobStore = blobStore;
        _identity = identity;
        _opts = options.Value;
    }

    public Task<bool> ShouldAcceptChunkAsync(string blobHash, string chapterId, CancellationToken ct = default)
    {
        if (_blobStore.Exists(new Blob.BlobHash(blobHash)))
            return Task.FromResult(false); // already have it

        // Gate 1: ring responsibility
        bool isResponsible = _ring.IsLocallyResponsible(blobHash, _opts.ActiveTargetReplicas);
        if (!isResponsible && !_opts.IsSuperSeeder)
            return Task.FromResult(false);

        // Gate 2: diversity constraint (unknown totalChunks → use 0 to bypass)
        if (!string.IsNullOrEmpty(chapterId) && !_diversityTracker.CanAcceptChunk(chapterId, 0))
            return Task.FromResult(false);

        return Task.FromResult(true);
    }

    public Task<bool> ShouldReplicateChunkAsync(string blobHash, string chapterId, CancellationToken ct = default)
    {
        if (!_blobStore.Exists(new Blob.BlobHash(blobHash)))
            return Task.FromResult(false); // can't push what we don't have

        // Only the ring leader (first responsible peer) drives replication to avoid storms
        var responsible = _ring.GetResponsiblePeers(blobHash, _opts.ActiveTargetReplicas);
        bool isLeader = responsible.Count > 0 &&
            responsible[0].NodeId.SequenceEqual(_identity.NodeId);

        if (!isLeader && !_opts.IsSuperSeeder)
            return Task.FromResult(false);

        int currentReplicas = _healthMonitor.EstimateReplicaCount(blobHash);
        return Task.FromResult(currentReplicas < _opts.ActiveTargetReplicas);
    }
}
