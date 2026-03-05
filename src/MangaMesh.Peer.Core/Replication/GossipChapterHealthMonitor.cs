using System.Collections.Concurrent;

namespace MangaMesh.Peer.Core.Replication;

/// <summary>
/// Tracks chapter health via gossip.  Replica counts are approximated using a
/// bounded HashSet per chunk (swappable for a true HyperLogLog in a future pass).
/// Chunk ownership hints are tracked with a simple per-chunk peer set that is
/// capped to avoid unbounded growth.
/// </summary>
public sealed class GossipChapterHealthMonitor : IChapterHealthMonitor
{
    // blobHash → set of peerIds that hold the chunk
    private readonly ConcurrentDictionary<string, HashSet<string>> _chunkOwners = new(StringComparer.OrdinalIgnoreCase);

    // chapterId → latest health state (from gossip or local computation)
    private readonly ConcurrentDictionary<string, ChapterHealthState> _healthStates = new(StringComparer.OrdinalIgnoreCase);

    private const int MaxOwnersPerChunk = 128; // cap to bound memory

    public void RecordChunkOwner(string blobHash, string peerId)
    {
        var owners = _chunkOwners.GetOrAdd(blobHash, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        lock (owners)
        {
            if (owners.Count < MaxOwnersPerChunk)
                owners.Add(peerId);
        }
    }

    public void MergeGossip(IEnumerable<ChapterHealthState> states)
    {
        foreach (ChapterHealthState state in states)
        {
            _healthStates.AddOrUpdate(
                state.ChapterId,
                state,
                (_, existing) => state.ObservedAtUtc > existing.ObservedAtUtc ? state : existing);
        }
    }

    public ChapterHealthState GetHealthState(
        string chapterId,
        string manifestHash,
        IEnumerable<string> chunkHashes,
        int minimumReplicas)
    {
        List<string> chunks = chunkHashes.ToList();
        int totalCount = chunks.Count;

        if (totalCount == 0)
        {
            return new ChapterHealthState(chapterId, manifestHash, 0, 0, 0, DateTime.UtcNow);
        }

        int rareCount = 0;
        int minReplica = int.MaxValue;

        foreach (string hash in chunks)
        {
            int replicas = EstimateReplicaCount(hash);
            if (replicas < minimumReplicas)
                rareCount++;
            if (replicas < minReplica)
                minReplica = replicas;
        }

        int overallEstimate = minReplica == int.MaxValue ? 0 : minReplica;

        var state = new ChapterHealthState(
            chapterId,
            manifestHash,
            overallEstimate,
            totalCount,
            rareCount,
            DateTime.UtcNow);

        _healthStates[chapterId] = state;
        return state;
    }

    public int EstimateReplicaCount(string blobHash)
    {
        if (!_chunkOwners.TryGetValue(blobHash, out HashSet<string>? owners))
            return 0;

        lock (owners)
            return owners.Count;
    }

    public bool PeerMayOwnChunk(string blobHash, string peerId)
    {
        if (!_chunkOwners.TryGetValue(blobHash, out HashSet<string>? owners))
            return false;

        lock (owners)
            return owners.Contains(peerId);
    }

    public IReadOnlyList<ChapterHealthState> GetAllHealthStates()
    {
        return _healthStates.Values.ToList();
    }
}
