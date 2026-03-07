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

    // peerId → (chapterId → BloomFilter)
    private readonly ConcurrentDictionary<string, Dictionary<string, MangaMesh.Peer.Core.Replication.BloomFilter>> _peerBloomFilters = new(StringComparer.OrdinalIgnoreCase);

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

    public void MergeGossip(string senderPeerId, Dictionary<string, byte[]> chunkBloomFilters, IEnumerable<ChapterHealthState> states)
    {
        if (!string.IsNullOrEmpty(senderPeerId) && chunkBloomFilters != null)
        {
            var peerFilters = _peerBloomFilters.GetOrAdd(senderPeerId, _ => new(StringComparer.OrdinalIgnoreCase));
            lock (peerFilters)
            {
                foreach (var kvp in chunkBloomFilters)
                {
                    peerFilters[kvp.Key] = new MangaMesh.Peer.Core.Replication.BloomFilter(kvp.Value, 3); // using 3 hash functions as standard
                }
            }
        }

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
            int replicas = EstimateReplicaCount(hash, chapterId);
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

        // Merge: keep whichever state has the higher replica estimate so gossip-informed
        // data from the seeder isn't overwritten by a non-seeder's local low estimate.
        _healthStates.AddOrUpdate(chapterId, state, (_, existing) =>
            existing.ReplicaEstimate > state.ReplicaEstimate ? existing : state);

        return _healthStates[chapterId];
    }

    public int EstimateReplicaCount(string blobHash, string? chapterId = null)
    {
        int count = 0;
        if (_chunkOwners.TryGetValue(blobHash, out HashSet<string>? owners))
        {
            lock (owners)
                count = owners.Count;
        }

        // Add remote peers derived from Bloom Filters
        if (!string.IsNullOrEmpty(chapterId))
        {
            foreach (var peerKvp in _peerBloomFilters)
            {
                string peerId = peerKvp.Key;
                // If local set already knows about this peer, don't double count
                if (owners != null)
                {
                    bool skip = false;
                    lock (owners) skip = owners.Contains(peerId);
                    if (skip) continue;
                }

                var filters = peerKvp.Value;
                lock (filters)
                {
                    if (filters.TryGetValue(chapterId, out var filter))
                    {
                        if (filter.Contains(blobHash))
                            count++;
                    }
                }
            }
        }

        return count;
    }

    public bool PeerMayOwnChunk(string blobHash, string peerId)
    {
        if (_chunkOwners.TryGetValue(blobHash, out HashSet<string>? owners))
        {
            lock (owners)
            {
                if (owners.Contains(peerId)) return true;
            }
        }

        // Check Bloom Filters
        if (_peerBloomFilters.TryGetValue(peerId, out var filters))
        {
            lock (filters)
            {
                // We don't have chapterId here, but we can check across all chapters this peer is seeding
                // In practice, this might have false collisons across chapters, but it's a "MayOwn" heuristic
                foreach (var filter in filters.Values)
                {
                    if (filter.Contains(blobHash))
                        return true;
                }
            }
        }

        return false;
    }

    public IReadOnlyList<ChapterHealthState> GetAllHealthStates()
    {
        return _healthStates.Values.ToList();
    }

    public byte[] GetLocalChunkBloomFilter(string chapterId)
    {
        // For local peer, generate a Bloom Filter of all chunks we own for this chapter
        // Since we don't index chunks by chapterId locally, we can extract from _healthStates if needed, 
        // or just scan _chunkOwners. However, since the peer has up to tens of thousands of chunks,
        // we should limit to chunks that belong to the chapter.
        // As a shortcut, the caller (PeerProfileGossipService) will just pass 
        // the healthStates it finds. To do this accurately, we need the actual chunk hashes.
        return Array.Empty<byte>(); // We will address generating this in PeerProfileGossipService where chunk hashes are accessible, or add a chapter-to-chunk index here.
    }
}
