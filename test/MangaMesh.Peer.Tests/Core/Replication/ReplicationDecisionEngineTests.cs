using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Replication;
using MangaMesh.Peer.Core.Transport;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Replication;

public class ReplicationDecisionEngineTests
{
    private static readonly byte[] LocalNodeId = new byte[32]; // all zeros

    private static readonly ReplicationOptions DefaultOptions = new()
    {
        IsSuperSeeder = false,
        ActiveTargetReplicas = 12,
        AbsoluteMinimumReplicas = 3
    };

    private static ReplicationDecisionEngine BuildEngine(
        bool blobExists = false,
        bool isRingResponsible = true,
        bool isRingLeader = true,
        bool canAcceptDiversity = true,
        int currentReplicas = 0,
        bool isSuperSeeder = false)
    {
        var opts = new ReplicationOptions
        {
            IsSuperSeeder = isSuperSeeder,
            ActiveTargetReplicas = DefaultOptions.ActiveTargetReplicas,
            AbsoluteMinimumReplicas = DefaultOptions.AbsoluteMinimumReplicas
        };

        var blob = new Mock<IBlobStore>();
        blob.Setup(b => b.Exists(It.IsAny<BlobHash>())).Returns(blobExists);

        var identity = new Mock<INodeIdentity>();
        identity.Setup(i => i.NodeId).Returns(LocalNodeId);

        // Consistent hash ring
        var ring = new Mock<IConsistentHashRing>();
        ring.Setup(r => r.IsLocallyResponsible(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(isRingResponsible);

        RoutingEntry leaderEntry = new()
        {
            NodeId = isRingLeader ? LocalNodeId : new byte[32].Select((_, i) => i == 0 ? (byte)0xFF : (byte)0).ToArray(),
            Address = new NodeAddress("127.0.0.1", 3000)
        };
        ring.Setup(r => r.GetResponsiblePeers(It.IsAny<string>(), It.IsAny<int>()))
            .Returns([leaderEntry]);

        // Health monitor
        var health = new Mock<IChapterHealthMonitor>();
        health.Setup(h => h.EstimateReplicaCount(It.IsAny<string>(), It.IsAny<string?>())).Returns(currentReplicas);

        // Diversity tracker
        var diversity = new Mock<IChapterDiversityTracker>();
        diversity.Setup(d => d.CanAcceptChunk(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(canAcceptDiversity);

        return new ReplicationDecisionEngine(
            ring.Object,
            health.Object,
            diversity.Object,
            blob.Object,
            identity.Object,
            Options.Create(opts));
    }

    // ── ShouldAcceptChunkAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ShouldAcceptChunk_AlreadyOwned_ReturnsFalse()
    {
        var engine = BuildEngine(blobExists: true, isRingResponsible: true, canAcceptDiversity: true);
        Assert.False(await engine.ShouldAcceptChunkAsync("hash1", "ch-1"));
    }

    [Fact]
    public async Task ShouldAcceptChunk_NotResponsible_NotSuperSeeder_ReturnsFalse()
    {
        var engine = BuildEngine(blobExists: false, isRingResponsible: false, isSuperSeeder: false);
        Assert.False(await engine.ShouldAcceptChunkAsync("hash1", "ch-1"));
    }

    [Fact]
    public async Task ShouldAcceptChunk_NotResponsible_IsSuperSeeder_ReturnsTrue()
    {
        var engine = BuildEngine(blobExists: false, isRingResponsible: false, isSuperSeeder: true, canAcceptDiversity: true);
        Assert.True(await engine.ShouldAcceptChunkAsync("hash1", "ch-1"));
    }

    [Fact]
    public async Task ShouldAcceptChunk_ResponsibleButDiversityDenied_IgnoresDiversityAndReturnsTrue()
    {
        var engine = BuildEngine(blobExists: false, isRingResponsible: true, canAcceptDiversity: false);
        Assert.True(await engine.ShouldAcceptChunkAsync("hash1", "ch-1"));
    }

    [Fact]
    public async Task ShouldAcceptChunk_AllGatesPass_ReturnsTrue()
    {
        var engine = BuildEngine(blobExists: false, isRingResponsible: true, canAcceptDiversity: true);
        Assert.True(await engine.ShouldAcceptChunkAsync("hash1", "ch-1"));
    }

    [Fact]
    public async Task ShouldAcceptChunk_EmptyChapterId_SkipsDiversityGate()
    {
        // Empty chapterId should bypass diversity check
        var engine = BuildEngine(blobExists: false, isRingResponsible: true, canAcceptDiversity: false);
        Assert.True(await engine.ShouldAcceptChunkAsync("hash1", ""));
    }

    // ── ShouldReplicateChunkAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ShouldReplicateChunk_DoesNotOwnBlob_ReturnsFalse()
    {
        var engine = BuildEngine(blobExists: false, isRingLeader: true, currentReplicas: 0);
        Assert.False(await engine.ShouldReplicateChunkAsync("hash1", "ch-1"));
    }

    [Fact]
    public async Task ShouldReplicateChunk_OwnsBlob_IsLeader_BelowTarget_ReturnsTrue()
    {
        var engine = BuildEngine(blobExists: true, isRingLeader: true, currentReplicas: 5);
        Assert.True(await engine.ShouldReplicateChunkAsync("hash1", "ch-1"));
    }

    [Fact]
    public async Task ShouldReplicateChunk_OwnsBlob_NotLeader_ReturnsFalse()
    {
        var engine = BuildEngine(blobExists: true, isRingLeader: false, currentReplicas: 5, isSuperSeeder: false);
        Assert.False(await engine.ShouldReplicateChunkAsync("hash1", "ch-1"));
    }

    [Fact]
    public async Task ShouldReplicateChunk_OwnsBlob_NotLeader_IsSuperSeeder_ReturnsTrue()
    {
        var engine = BuildEngine(blobExists: true, isRingLeader: false, currentReplicas: 5, isSuperSeeder: true);
        Assert.True(await engine.ShouldReplicateChunkAsync("hash1", "ch-1"));
    }

    [Fact]
    public async Task ShouldReplicateChunk_OwnsBlob_IsLeader_AlreadyAtTarget_ReturnsFalse()
    {
        // currentReplicas >= ActiveTargetReplicas (12)
        var engine = BuildEngine(blobExists: true, isRingLeader: true, currentReplicas: 12);
        Assert.False(await engine.ShouldReplicateChunkAsync("hash1", "ch-1"));
    }

    [Fact]
    public async Task ShouldReplicateChunk_OwnsBlob_IsLeader_AboveTarget_ReturnsFalse()
    {
        var engine = BuildEngine(blobExists: true, isRingLeader: true, currentReplicas: 20);
        Assert.False(await engine.ShouldReplicateChunkAsync("hash1", "ch-1"));
    }
}
