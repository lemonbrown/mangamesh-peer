namespace MangaMesh.Peer.Core.Replication;

public sealed record ChapterHealthState(
    string ChapterId,
    string ManifestHash,
    int ReplicaEstimate,
    int TotalChunkCount,
    int RareChunkCount,
    DateTime ObservedAtUtc
)
{
    public bool IsHealthy(int minimumReplicas) =>
        RareChunkCount == 0 && ReplicaEstimate >= minimumReplicas;
}
