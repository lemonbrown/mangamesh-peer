using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Replication;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Replication;

public class ScoredEvictionPolicyTests
{
    private static readonly EvictionOptions DefaultEviction = new()
    {
        PopularityWeight = 0.4,
        RarityWeight = 0.4,
        AgeWeight = 0.2,
        PopularityWindowDays = 30
    };

    private static readonly ReplicationOptions DefaultReplication = new()
    {
        AbsoluteMinimumReplicas = 3,
        ActiveTargetReplicas = 12
    };

    private static ScoredEvictionPolicy BuildPolicy(IChapterHealthMonitor? monitor = null)
    {
        monitor ??= new Mock<IChapterHealthMonitor>().Object;
        return new ScoredEvictionPolicy(
            monitor,
            Options.Create(DefaultEviction),
            Options.Create(DefaultReplication));
    }

    private static async Task<List<EvictionCandidate>> CollectAsync(
        IAsyncEnumerable<EvictionCandidate> candidates)
    {
        var list = new List<EvictionCandidate>();
        await foreach (var c in candidates)
            list.Add(c);
        return list;
    }

    // ── Empty input ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEvictionCandidates_EmptyInput_YieldsNothing()
    {
        var policy = BuildPolicy();
        var results = await CollectAsync(policy.GetEvictionCandidatesAsync(
            [], _ => 0, _ => DateTime.UtcNow, bytesNeeded: 100));
        Assert.Empty(results);
    }

    // ── Protected blobs excluded ───────────────────────────────────────────────

    [Fact]
    public async Task GetEvictionCandidates_ProtectedBlob_IsExcluded()
    {
        // replica < AbsoluteMinimumReplicas (3) AND > 0 → protected
        var monitor = new Mock<IChapterHealthMonitor>();
        monitor.Setup(m => m.EstimateReplicaCount(It.IsAny<string>())).Returns(2);

        var policy = BuildPolicy(monitor.Object);
        var blobs = new[] { new BlobHash("protected-blob") };

        var results = await CollectAsync(policy.GetEvictionCandidatesAsync(
            blobs, _ => 1000, _ => DateTime.UtcNow.AddDays(-1), bytesNeeded: 999_999));

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetEvictionCandidates_ZeroReplicaBlob_IsNotProtected()
    {
        // replica == 0 means unknown — not protected
        var monitor = new Mock<IChapterHealthMonitor>();
        monitor.Setup(m => m.EstimateReplicaCount(It.IsAny<string>())).Returns(0);

        var policy = BuildPolicy(monitor.Object);
        var blobs = new[] { new BlobHash("unknown-blob") };

        var results = await CollectAsync(policy.GetEvictionCandidatesAsync(
            blobs, _ => 1000, _ => DateTime.UtcNow.AddDays(-31), bytesNeeded: 999_999));

        Assert.Single(results);
    }

    // ── Ordering: lowest score first ───────────────────────────────────────────

    [Fact]
    public async Task GetEvictionCandidates_OldAccessedBlob_EvictedBeforeRecent()
    {
        var monitor = new Mock<IChapterHealthMonitor>();
        monitor.Setup(m => m.EstimateReplicaCount(It.IsAny<string>())).Returns(10);

        var policy = BuildPolicy(monitor.Object);
        var blobs = new[]
        {
            new BlobHash("old-blob"),   // accessed long ago → low popularity → low score → evicted first
            new BlobHash("new-blob")    // accessed recently → high popularity → high score → kept
        };

        DateTime lastAccessLookup(BlobHash h) =>
            h.Value == "old-blob" ? DateTime.UtcNow.AddDays(-60) : DateTime.UtcNow;

        var results = await CollectAsync(policy.GetEvictionCandidatesAsync(
            blobs, _ => 1000, lastAccessLookup, bytesNeeded: 999_999));

        Assert.Equal(2, results.Count);
        Assert.Equal("old-blob", results[0].BlobHash); // lower score = evict first
    }

    // ── bytes needed stops iteration ───────────────────────────────────────────

    [Fact]
    public async Task GetEvictionCandidates_StopsAfterBytesNeededFreed()
    {
        var monitor = new Mock<IChapterHealthMonitor>();
        monitor.Setup(m => m.EstimateReplicaCount(It.IsAny<string>())).Returns(10);

        var policy = BuildPolicy(monitor.Object);
        var blobs = Enumerable.Range(0, 10).Select(i => new BlobHash($"blob-{i}")).ToArray();

        // Each blob is 1000 bytes; need to free 2500 → should yield 3 blobs (3000 freed ≥ 2500)
        var results = await CollectAsync(policy.GetEvictionCandidatesAsync(
            blobs, _ => 1000, _ => DateTime.UtcNow.AddDays(-10), bytesNeeded: 2500));

        Assert.True(results.Count <= 3, $"Expected at most 3 candidates, got {results.Count}");
        Assert.True(results.Sum(c => c.SizeBytes) >= 2500 || results.Count == blobs.Length);
    }

    // ── Candidate fields are populated ────────────────────────────────────────

    [Fact]
    public async Task GetEvictionCandidates_CandidateHasCorrectSizeAndHash()
    {
        var monitor = new Mock<IChapterHealthMonitor>();
        monitor.Setup(m => m.EstimateReplicaCount(It.IsAny<string>())).Returns(10);

        var policy = BuildPolicy(monitor.Object);
        var blobs = new[] { new BlobHash("my-blob") };

        var results = await CollectAsync(policy.GetEvictionCandidatesAsync(
            blobs, _ => 5000, _ => DateTime.UtcNow.AddDays(-30), bytesNeeded: 999_999));

        Assert.Single(results);
        Assert.Equal("my-blob", results[0].BlobHash);
        Assert.Equal(5000, results[0].SizeBytes);
        Assert.False(results[0].IsProtected);
    }
}
