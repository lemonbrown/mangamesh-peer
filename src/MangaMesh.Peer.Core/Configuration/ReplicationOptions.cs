using MangaMesh.Peer.Core.Replication;

namespace MangaMesh.Peer.Core.Configuration;

public sealed class ReplicationOptions
{
    public bool Enabled { get; set; } = true;
    public bool IsSuperSeeder { get; set; } = false;
    public BandwidthClass BandwidthClass { get; set; } = BandwidthClass.Medium;

    // Replica targets by chapter age tier
    public int NewReleaseTargetReplicas { get; set; } = 25;
    public int ActiveTargetReplicas { get; set; } = 12;
    public int OlderTargetReplicas { get; set; } = 5;
    public int ArchivalMinimumReplicas { get; set; } = 3;
    public int AbsoluteMinimumReplicas { get; set; } = 3;

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
