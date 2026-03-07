using MangaMesh.Peer.Core.Replication;

namespace MangaMesh.Peer.Core.Configuration;

public sealed class ReplicationOptions
{
    public bool Enabled { get; set; } = true;
    public bool IsSuperSeeder { get; set; } = false;
    public bool IsFullSeeder { get; set; } = false;
    public BandwidthClass BandwidthClass { get; set; } = BandwidthClass.Medium;

    // Dynamic replication formula: K = clamp(swarmSize / RedundancyFactor, 1, MaxTargetReplicas)
    // New releases get K * NewReleaseBoost (still capped at MaxTargetReplicas).
    // The seeder is always the guaranteed full copy; MinimumReplicas is always 1.
    public int RedundancyFactor { get; set; } = 4;
    public int MaxTargetReplicas { get; set; } = 8;
    public double NewReleaseBoost { get; set; } = 1.5;

    // Age thresholds (days)
    public int NewReleaseAgeDays { get; set; } = 7;
    public int ActiveAgeDays { get; set; } = 90;

    // Max fraction of a chapter's chunks this peer may hold
    public double MaxChunksPerPeerPerChapterRatio { get; set; } = 0.20;

    // Background service intervals
    public int GossipIntervalSeconds { get; set; } = 60;
    public int RepairScanIntervalSeconds { get; set; } = 300;

    // Virtual nodes per peer on the consistent hash ring
    public int ConsistentHashVirtualNodes { get; set; } = 150;
}
