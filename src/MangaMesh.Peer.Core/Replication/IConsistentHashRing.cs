using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Replication;

public interface IConsistentHashRing
{
    /// <summary>
    /// Returns the <paramref name="replicaCount"/> peers responsible for storing this chunk,
    /// determined deterministically from the hash ring without coordination.
    /// </summary>
    IReadOnlyList<RoutingEntry> GetResponsiblePeers(string chunkBlobHash, int replicaCount);

    /// <summary>
    /// True if the local node is one of the <paramref name="replicaCount"/> responsible peers
    /// for this chunk on the current ring.
    /// </summary>
    bool IsLocallyResponsible(string chunkBlobHash, int replicaCount);
}
