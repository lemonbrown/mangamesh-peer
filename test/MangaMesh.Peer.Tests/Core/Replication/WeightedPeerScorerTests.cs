using MangaMesh.Peer.Core.Replication;
using MangaMesh.Peer.Core.Transport;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Replication;

public class WeightedPeerScorerTests
{
    private readonly WeightedPeerScorer _scorer = new();

    private static RoutingEntry MakeEntry(
        string name,
        long capacityBytes = 100_000_000,
        long usedBytes = 0,
        byte uptimeScore = 100,
        byte bandwidthClass = (byte)BandwidthClass.High)
    {
        return new RoutingEntry
        {
            NodeId = [.. System.Text.Encoding.UTF8.GetBytes(name)],
            Address = new NodeAddress("127.0.0.1", 3000),
            LastSeenUtc = DateTime.UtcNow,
            StorageCapacityBytes = capacityBytes,
            StorageUsedBytes = usedBytes,
            UptimeScore = uptimeScore,
            BandwidthClass = bandwidthClass
        };
    }

    // ── Ordering ───────────────────────────────────────────────────────────────

    [Fact]
    public void RankCandidates_HighStoragePeer_RankedAboveLowStorage()
    {
        var highFree = MakeEntry("high", capacityBytes: 1_000_000, usedBytes: 0);
        var lowFree = MakeEntry("low", capacityBytes: 1_000_000, usedBytes: 900_000);

        var result = _scorer.RankCandidates([lowFree, highFree], 2);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].FreeStorageBytes >= result[1].FreeStorageBytes);
    }

    [Fact]
    public void RankCandidates_HighUptimePeer_RankedAboveLowUptime()
    {
        // Same storage and bandwidth; uptime differs
        var highUptime = MakeEntry("high", uptimeScore: 100);
        var lowUptime = MakeEntry("low", uptimeScore: 10);

        var result = _scorer.RankCandidates([lowUptime, highUptime], 2);

        Assert.Equal("high", System.Text.Encoding.UTF8.GetString(result[0].NodeId));
    }

    [Fact]
    public void RankCandidates_HighBandwidthPeer_RankedAboveLowBandwidth()
    {
        // Same storage and uptime; bandwidth differs
        var highBW = MakeEntry("high", bandwidthClass: (byte)BandwidthClass.High);
        var lowBW = MakeEntry("low", bandwidthClass: (byte)BandwidthClass.Low);

        var result = _scorer.RankCandidates([lowBW, highBW], 2);

        Assert.Equal("high", System.Text.Encoding.UTF8.GetString(result[0].NodeId));
    }

    // ── Count limiting ─────────────────────────────────────────────────────────

    [Fact]
    public void RankCandidates_LimitsToRequestedCount()
    {
        var entries = Enumerable.Range(0, 10).Select(i => MakeEntry($"peer-{i}")).ToList();
        var result = _scorer.RankCandidates(entries, 3);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void RankCandidates_FewerCandidates_ReturnsAll()
    {
        var entries = Enumerable.Range(0, 2).Select(i => MakeEntry($"peer-{i}")).ToList();
        var result = _scorer.RankCandidates(entries, 10);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void RankCandidates_EmptyInput_ReturnsEmpty()
    {
        var result = _scorer.RankCandidates([], 5);
        Assert.Empty(result);
    }

    // ── Unknown capacity fallback ──────────────────────────────────────────────

    [Fact]
    public void RankCandidates_ZeroCapacity_DoesNotCrash()
    {
        var zeroCapacity = MakeEntry("zero", capacityBytes: 0, usedBytes: 0);
        var result = _scorer.RankCandidates([zeroCapacity], 1);
        Assert.Single(result);
    }

    // ── No duplicate results ───────────────────────────────────────────────────

    [Fact]
    public void RankCandidates_NoDuplicateEntries()
    {
        var entries = Enumerable.Range(0, 5).Select(i => MakeEntry($"peer-{i}")).ToList();
        var result = _scorer.RankCandidates(entries, 5);
        var ids = result.Select(e => System.Text.Encoding.UTF8.GetString(e.NodeId)).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }
}
