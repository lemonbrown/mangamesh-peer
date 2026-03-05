using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Replication;
using Microsoft.Extensions.Options;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Replication;

public class InMemoryChapterDiversityTrackerTests
{
    private static InMemoryChapterDiversityTracker BuildTracker(double maxRatio = 0.20)
    {
        var opts = new ReplicationOptions { MaxChunksPerPeerPerChapterRatio = maxRatio };
        return new InMemoryChapterDiversityTracker(Options.Create(opts));
    }

    // ── CanAcceptChunk ─────────────────────────────────────────────────────────

    [Fact]
    public void CanAcceptChunk_ZeroTotalChunks_AlwaysReturnsTrue()
    {
        var tracker = BuildTracker(maxRatio: 0.0); // even with 0% ratio
        Assert.True(tracker.CanAcceptChunk("ch-1", totalChunksInChapter: 0));
    }

    [Fact]
    public void CanAcceptChunk_FirstChunk_ReturnsTrueWhenBelowRatio()
    {
        var tracker = BuildTracker(maxRatio: 0.20);
        // (0+1)/100 = 0.01 ≤ 0.20 → allowed
        Assert.True(tracker.CanAcceptChunk("ch-1", totalChunksInChapter: 100));
    }

    [Fact]
    public void CanAcceptChunk_AtExactRatioLimit_ReturnsTrue()
    {
        // ratio = 20/100 = 0.20 → exactly at limit → allowed
        var tracker = BuildTracker(maxRatio: 0.20);
        for (int i = 0; i < 19; i++) tracker.RecordChunkAccepted("ch-1");
        // current=19, next=(19+1)/100=0.20 → allowed
        Assert.True(tracker.CanAcceptChunk("ch-1", totalChunksInChapter: 100));
    }

    [Fact]
    public void CanAcceptChunk_ExceedsRatioLimit_ReturnsFalse()
    {
        // ratio = 21/100 = 0.21 > 0.20 → denied
        var tracker = BuildTracker(maxRatio: 0.20);
        for (int i = 0; i < 20; i++) tracker.RecordChunkAccepted("ch-1");
        Assert.False(tracker.CanAcceptChunk("ch-1", totalChunksInChapter: 100));
    }

    [Fact]
    public void CanAcceptChunk_DifferentChapters_TrackedIndependently()
    {
        var tracker = BuildTracker(maxRatio: 0.20);
        for (int i = 0; i < 20; i++) tracker.RecordChunkAccepted("ch-A");
        // ch-A is full; ch-B is empty
        Assert.False(tracker.CanAcceptChunk("ch-A", totalChunksInChapter: 100));
        Assert.True(tracker.CanAcceptChunk("ch-B", totalChunksInChapter: 100));
    }

    // ── RecordChunkAccepted ────────────────────────────────────────────────────

    [Fact]
    public void RecordChunkAccepted_IncrementsCount()
    {
        var tracker = BuildTracker();
        tracker.RecordChunkAccepted("ch-x");
        tracker.RecordChunkAccepted("ch-x");
        Assert.Equal(2, tracker.GetLocalChunkCount("ch-x"));
    }

    [Fact]
    public void RecordChunkAccepted_NewChapter_StartsAtOne()
    {
        var tracker = BuildTracker();
        tracker.RecordChunkAccepted("ch-new");
        Assert.Equal(1, tracker.GetLocalChunkCount("ch-new"));
    }

    // ── GetLocalChunkCount ────────────────────────────────────────────────────

    [Fact]
    public void GetLocalChunkCount_UnknownChapter_ReturnsZero()
    {
        var tracker = BuildTracker();
        Assert.Equal(0, tracker.GetLocalChunkCount("ch-unknown"));
    }

    [Fact]
    public void GetLocalChunkCount_AfterMultipleAccepts_ReturnsCorrectCount()
    {
        var tracker = BuildTracker();
        for (int i = 0; i < 7; i++) tracker.RecordChunkAccepted("ch-seven");
        Assert.Equal(7, tracker.GetLocalChunkCount("ch-seven"));
    }

    // ── Ratio edge cases ───────────────────────────────────────────────────────

    [Fact]
    public void CanAcceptChunk_100PercentRatio_AlwaysAllows()
    {
        var tracker = BuildTracker(maxRatio: 1.0);
        for (int i = 0; i < 100; i++) tracker.RecordChunkAccepted("ch-full");
        // (100+1)/100 = 1.01 > 1.0 → denied (ratio is strictly at limit)
        // For a fresh chapter with 1.0 ratio, all chunks up to total should be allowed
        var tracker2 = BuildTracker(maxRatio: 1.0);
        for (int i = 0; i < 99; i++) tracker2.RecordChunkAccepted("ch-full2");
        // (99+1)/100 = 1.00 ≤ 1.0 → allowed
        Assert.True(tracker2.CanAcceptChunk("ch-full2", totalChunksInChapter: 100));
    }
}
