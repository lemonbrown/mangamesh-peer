namespace MangaMesh.Peer.Core.Replication;

public interface IReplicationDecisionEngine
{
    /// <summary>
    /// Determines whether the local node should accept and store this chunk.
    /// Checks ring responsibility, available storage, and diversity constraint.
    /// </summary>
    Task<bool> ShouldAcceptChunkAsync(string blobHash, string chapterId, CancellationToken ct = default);

    /// <summary>
    /// Determines whether the local node should actively push this chunk to new peers.
    /// True when local node is ring leader and estimated replicas are below target.
    /// </summary>
    Task<bool> ShouldReplicateChunkAsync(string blobHash, string chapterId, CancellationToken ct = default);
}
