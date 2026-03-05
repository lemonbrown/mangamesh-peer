namespace MangaMesh.Peer.Core.Replication;

public sealed record EvictionCandidate(
    string BlobHash,
    long SizeBytes,
    double Score,       // lower = evict first
    int ReplicaCount,
    bool IsProtected    // never evict when true (replica count < minimum)
);
