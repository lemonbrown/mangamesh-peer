namespace MangaMesh.Peer.Core.Replication;

public sealed record ChunkReplicaTarget(int TargetReplicas, int MinimumReplicas);
