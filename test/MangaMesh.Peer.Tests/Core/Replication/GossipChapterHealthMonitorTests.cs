using MangaMesh.Peer.Core.Replication;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Replication;

public class GossipChapterHealthMonitorTests
{
    private readonly GossipChapterHealthMonitor _monitor = new();

    // ── RecordChunkOwner / EstimateReplicaCount ────────────────────────────────

    [Fact]
    public void EstimateReplicaCount_UnknownBlob_ReturnsZero()
    {
        Assert.Equal(0, _monitor.EstimateReplicaCount("unknown-hash"));
    }

    [Fact]
    public void RecordChunkOwner_SinglePeer_ReturnsOne()
    {
        _monitor.RecordChunkOwner("blob1", "peer-a");
        Assert.Equal(1, _monitor.EstimateReplicaCount("blob1"));
    }

    [Fact]
    public void RecordChunkOwner_MultiplePeers_CountsDistinct()
    {
        _monitor.RecordChunkOwner("blob2", "peer-a");
        _monitor.RecordChunkOwner("blob2", "peer-b");
        _monitor.RecordChunkOwner("blob2", "peer-c");
        Assert.Equal(3, _monitor.EstimateReplicaCount("blob2"));
    }

    [Fact]
    public void RecordChunkOwner_SamePeerTwice_CountedOnce()
    {
        _monitor.RecordChunkOwner("blob3", "peer-a");
        _monitor.RecordChunkOwner("blob3", "peer-a");
        Assert.Equal(1, _monitor.EstimateReplicaCount("blob3"));
    }

    [Fact]
    public void RecordChunkOwner_CaseInsensitivePeerId_TreatedAsSame()
    {
        _monitor.RecordChunkOwner("blob4", "PEER-A");
        _monitor.RecordChunkOwner("blob4", "peer-a");
        Assert.Equal(1, _monitor.EstimateReplicaCount("blob4"));
    }

    // ── PeerMayOwnChunk ────────────────────────────────────────────────────────

    [Fact]
    public void PeerMayOwnChunk_AfterRecord_ReturnsTrue()
    {
        _monitor.RecordChunkOwner("blob5", "peer-x");
        Assert.True(_monitor.PeerMayOwnChunk("blob5", "peer-x"));
    }

    [Fact]
    public void PeerMayOwnChunk_UnrecordedPeer_ReturnsFalse()
    {
        _monitor.RecordChunkOwner("blob6", "peer-a");
        Assert.False(_monitor.PeerMayOwnChunk("blob6", "peer-unknown"));
    }

    [Fact]
    public void PeerMayOwnChunk_UnknownBlob_ReturnsFalse()
    {
        Assert.False(_monitor.PeerMayOwnChunk("nonexistent-blob", "any-peer"));
    }

    // ── MergeGossip / GetAllHealthStates ──────────────────────────────────────

    [Fact]
    public void MergeGossip_AddsNewState()
    {
        var state = new ChapterHealthState("ch-1", "hash-1", 5, 10, 0, DateTime.UtcNow);
        _monitor.MergeGossip([state]);

        var all = _monitor.GetAllHealthStates();
        Assert.Single(all);
        Assert.Equal("ch-1", all[0].ChapterId);
    }

    [Fact]
    public void MergeGossip_NewerStateReplaces_OlderState()
    {
        var older = new ChapterHealthState("ch-2", "hash-2", 2, 10, 0, DateTime.UtcNow.AddMinutes(-10));
        var newer = new ChapterHealthState("ch-2", "hash-2", 8, 10, 0, DateTime.UtcNow);
        _monitor.MergeGossip([older]);
        _monitor.MergeGossip([newer]);

        var all = _monitor.GetAllHealthStates();
        Assert.Single(all);
        Assert.Equal(8, all[0].ReplicaEstimate);
    }

    [Fact]
    public void MergeGossip_OlderStateDoes_NotReplaceNewer()
    {
        var newer = new ChapterHealthState("ch-3", "hash-3", 8, 10, 0, DateTime.UtcNow);
        var older = new ChapterHealthState("ch-3", "hash-3", 2, 10, 0, DateTime.UtcNow.AddMinutes(-10));
        _monitor.MergeGossip([newer]);
        _monitor.MergeGossip([older]);

        var all = _monitor.GetAllHealthStates();
        Assert.Equal(8, all[0].ReplicaEstimate); // still newest
    }

    [Fact]
    public void MergeGossip_MultipleChapters_AllStored()
    {
        _monitor.MergeGossip([
            new ChapterHealthState("ch-A", "h1", 3, 10, 0, DateTime.UtcNow),
            new ChapterHealthState("ch-B", "h2", 5, 10, 0, DateTime.UtcNow)
        ]);

        var all = _monitor.GetAllHealthStates();
        Assert.Equal(2, all.Count);
    }

    // ── GetHealthState ─────────────────────────────────────────────────────────

    [Fact]
    public void GetHealthState_NoChunks_ReturnsZeroCounts()
    {
        var state = _monitor.GetHealthState("ch-empty", "hash-e", [], 3);
        Assert.Equal(0, state.TotalChunkCount);
        Assert.Equal(0, state.RareChunkCount);
        Assert.Equal(0, state.ReplicaEstimate);
    }

    [Fact]
    public void GetHealthState_AllChunksWellReplicated_ZeroRareChunks()
    {
        // Record 3 owners for each chunk
        foreach (var chunk in new[] { "c1", "c2", "c3" })
        {
            _monitor.RecordChunkOwner(chunk, "peer-1");
            _monitor.RecordChunkOwner(chunk, "peer-2");
            _monitor.RecordChunkOwner(chunk, "peer-3");
        }

        var state = _monitor.GetHealthState("chapter-1", "manifest-1", ["c1", "c2", "c3"], minimumReplicas: 3);
        Assert.Equal(3, state.TotalChunkCount);
        Assert.Equal(0, state.RareChunkCount);
        Assert.True(state.IsHealthy(3));
    }

    [Fact]
    public void GetHealthState_SomeChunksBelowMinimum_CountsRareChunks()
    {
        // c1 has 3 replicas, c2 has only 1
        _monitor.RecordChunkOwner("c1-rare", "peer-1");
        _monitor.RecordChunkOwner("c1-rare", "peer-2");
        _monitor.RecordChunkOwner("c1-rare", "peer-3");
        _monitor.RecordChunkOwner("c2-rare", "peer-1");

        var state = _monitor.GetHealthState("ch-rare", "mh-rare", ["c1-rare", "c2-rare"], minimumReplicas: 3);
        Assert.Equal(2, state.TotalChunkCount);
        Assert.Equal(1, state.RareChunkCount); // c2 is rare
    }

    [Fact]
    public void GetHealthState_ReplicaEstimate_IsMinimumAcrossChunks()
    {
        // c1=3 replicas, c2=1 replica → estimate = 1 (the minimum)
        _monitor.RecordChunkOwner("ca", "peer-1");
        _monitor.RecordChunkOwner("ca", "peer-2");
        _monitor.RecordChunkOwner("ca", "peer-3");
        _monitor.RecordChunkOwner("cb", "peer-1");

        var state = _monitor.GetHealthState("ch-min", "mh-min", ["ca", "cb"], minimumReplicas: 3);
        Assert.Equal(1, state.ReplicaEstimate);
    }
}
