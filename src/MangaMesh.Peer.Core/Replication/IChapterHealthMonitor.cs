namespace MangaMesh.Peer.Core.Replication;

public interface IChapterHealthMonitor
{
    /// <summary>Records that <paramref name="peerId"/> holds the given chunk.</summary>
    void RecordChunkOwner(string blobHash, string peerId);

    /// <summary>Merges gossipped chapter health observations into local state.</summary>
    void MergeGossip(IEnumerable<ChapterHealthState> states);

    /// <summary>
    /// Builds a health snapshot for one chapter from locally known chunk owner data.
    /// </summary>
    ChapterHealthState GetHealthState(
        string chapterId,
        string manifestHash,
        IEnumerable<string> chunkHashes,
        int minimumReplicas);

    /// <summary>Estimates the number of peers that hold this chunk (HyperLogLog approximation).</summary>
    int EstimateReplicaCount(string blobHash);

    /// <summary>
    /// Returns true if the given peer *may* own this chunk (Bloom filter — no false negatives,
    /// possible false positives).
    /// </summary>
    bool PeerMayOwnChunk(string blobHash, string peerId);

    /// <summary>Returns all chapter health states known to this monitor.</summary>
    IReadOnlyList<ChapterHealthState> GetAllHealthStates();
}
