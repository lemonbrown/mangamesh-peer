namespace MangaMesh.Peer.Core.Replication;

public enum BandwidthClass : byte
{
    Low = 0,    // < 10 Mbps upload
    Medium = 1, // 10–100 Mbps upload
    High = 2    // > 100 Mbps upload
}
