using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Core.Replication;

/// <summary>
/// Pulls chunks from the DHT network and pushes them to ring-assigned peers.
/// </summary>
public sealed class ChunkReplicationExecutor : IReplicationExecutor
{
    private readonly IBlobStore _blobStore;
    private readonly IDhtNode _dhtNode;
    private readonly IConsistentHashRing _ring;
    private readonly IPeerScorer _scorer;
    private readonly IChapterHealthMonitor _healthMonitor;
    private readonly INodeIdentity _identity;
    private readonly ReplicationOptions _opts;
    private readonly ILogger<ChunkReplicationExecutor> _logger;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public ChunkReplicationExecutor(
        IBlobStore blobStore,
        IDhtNode dhtNode,
        IConsistentHashRing ring,
        IPeerScorer scorer,
        IChapterHealthMonitor healthMonitor,
        INodeIdentity identity,
        IOptions<ReplicationOptions> options,
        ILogger<ChunkReplicationExecutor> logger)
    {
        _blobStore = blobStore;
        _dhtNode = dhtNode;
        _ring = ring;
        _scorer = scorer;
        _healthMonitor = healthMonitor;
        _identity = identity;
        _opts = options.Value;
        _logger = logger;
    }

    public async Task<bool> ReplicateChunkAsync(string blobHash, CancellationToken ct = default)
    {
        var hash = new BlobHash(blobHash);

        if (_blobStore.Exists(hash))
        {
            await AnnounceOwnershipAsync(blobHash);
            return true;
        }

        byte[] hashBytes;
        try { hashBytes = Convert.FromHexString(blobHash); }
        catch { return false; }

        var providers = await _dhtNode.FindValueWithAddressAsync(hashBytes);
        if (providers.Count == 0)
        {
            _logger.LogWarning("No providers found for chunk {Hash}", blobHash);
            return false;
        }

        foreach (var provider in providers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var response = await _dhtNode.SendContentRequestAsync(
                    provider.Address,
                    new GetBlob { BlobHash = blobHash },
                    RequestTimeout);

                if (response is BlobData blobData && blobData.Data.Length > 0)
                {
                    using var stream = new MemoryStream(blobData.Data);
                    await _blobStore.PutAsync(stream);
                    await AnnounceOwnershipAsync(blobHash);
                    _healthMonitor.RecordChunkOwner(blobHash, LocalPeerId());
                    _logger.LogDebug("Replicated chunk {Hash} from {Host}", blobHash, provider.Address.Host);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch chunk {Hash} from {Host}", blobHash, provider.Address.Host);
            }
        }

        return false;
    }

    public async Task<bool> PushChunkToPeerAsync(
        string blobHash,
        string chapterId,
        RoutingEntry target,
        int priority = 0,
        CancellationToken ct = default)
    {
        if (target.NodeId.SequenceEqual(_identity.NodeId))
            return false; // don't push to self

        try
        {
            var response = await _dhtNode.SendContentRequestAsync(
                target.Address,
                new ReplicateChunk { BlobHash = blobHash, ChapterId = chapterId, Priority = priority },
                RequestTimeout);

            if (response is ReplicateChunkAck ack)
            {
                if (ack.Accepted)
                {
                    string targetId = Convert.ToHexString(target.NodeId).ToLowerInvariant();
                    _healthMonitor.RecordChunkOwner(blobHash, targetId);
                    _logger.LogDebug("Peer {NodeId} accepted chunk {Hash}", targetId[..8], blobHash[..8]);
                }
                return ack.Accepted;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push of chunk {Hash} to {Host} failed", blobHash[..8], target.Address.Host);
            return false;
        }
    }

    public async Task PushToRingPeersAsync(
        string blobHash,
        string chapterId,
        int targetReplicas,
        CancellationToken ct = default)
    {
        int currentReplicas = _healthMonitor.EstimateReplicaCount(blobHash);
        int needed = targetReplicas - currentReplicas;

        if (needed <= 0)
            return;

        var candidates = _ring.GetResponsiblePeers(blobHash, targetReplicas);
        string localId = LocalPeerId();

        // Filter out local node and peers that probably already have it
        var targets = candidates
            .Where(e => !Convert.ToHexString(e.NodeId).Equals(localId, StringComparison.OrdinalIgnoreCase))
            .Where(e => !_healthMonitor.PeerMayOwnChunk(blobHash, Convert.ToHexString(e.NodeId).ToLowerInvariant()))
            .ToList();

        var ranked = _scorer.RankCandidates(targets, needed);

        foreach (RoutingEntry target in ranked)
        {
            ct.ThrowIfCancellationRequested();
            await PushChunkToPeerAsync(blobHash, chapterId, target, ct: ct);
        }
    }

    private Task AnnounceOwnershipAsync(string blobHash)
    {
        try
        {
            byte[] bytes = Convert.FromHexString(blobHash);
            return _dhtNode.StoreAsync(bytes);
        }
        catch
        {
            return Task.CompletedTask;
        }
    }

    private string LocalPeerId() =>
        Convert.ToHexString(_identity.NodeId).ToLowerInvariant();
}
