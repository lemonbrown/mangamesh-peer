using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Replication;

public interface IReplicationExecutor
{
    /// <summary>
    /// Fetches the chunk from any available peer, stores it locally, and announces
    /// ownership in the DHT.
    /// </summary>
    Task<bool> ReplicateChunkAsync(string blobHash, CancellationToken ct = default);

    /// <summary>
    /// Sends a ReplicateChunk push request to a specific target peer.
    /// Returns true if the peer accepted the chunk.
    /// </summary>
    Task<bool> PushChunkToPeerAsync(
        string blobHash,
        string chapterId,
        RoutingEntry target,
        int priority = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Pushes this chunk to all ring-responsible peers that do not yet appear to own it.
    /// Fire-and-forget friendly: errors per peer are logged and skipped.
    /// </summary>
    Task PushToRingPeersAsync(
        string blobHash,
        string chapterId,
        int targetReplicas,
        CancellationToken ct = default);
}
