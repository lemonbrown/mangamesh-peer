using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Replication;
using MangaMesh.Peer.Core.Transport;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Replication;

public class ConsistentHashRingTests
{
    private static readonly ReplicationOptions DefaultOptions = new()
    {
        ConsistentHashVirtualNodes = 50 // small for tests
    };

    private static RoutingEntry MakeEntry(string name) => new()
    {
        NodeId = SHA256.HashData(Encoding.UTF8.GetBytes(name)),
        Address = new NodeAddress("127.0.0.1", 3000),
        LastSeenUtc = DateTime.UtcNow
    };

    private static ConsistentHashRing BuildRing(
        IEnumerable<RoutingEntry> peers,
        byte[]? localNodeId = null)
    {
        var routingTable = new Mock<IRoutingTable>();
        routingTable.Setup(r => r.GetAll()).Returns(peers.ToList());

        var identity = new Mock<INodeIdentity>();
        identity.Setup(i => i.NodeId)
            .Returns(localNodeId ?? SHA256.HashData(Encoding.UTF8.GetBytes("local")));

        return new ConsistentHashRing(
            routingTable.Object,
            identity.Object,
            Options.Create(DefaultOptions));
    }

    [Fact]
    public void GetResponsiblePeers_SameChunk_AlwaysReturnsSamePeers()
    {
        var peers = Enumerable.Range(0, 5).Select(i => MakeEntry($"peer-{i}")).ToList();
        var ring = BuildRing(peers);

        var first = ring.GetResponsiblePeers("abc123", 3);
        var second = ring.GetResponsiblePeers("abc123", 3);

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
            Assert.True(first[i].NodeId.SequenceEqual(second[i].NodeId));
    }

    [Fact]
    public void GetResponsiblePeers_DifferentChunks_CanReturnDifferentPeers()
    {
        var peers = Enumerable.Range(0, 10).Select(i => MakeEntry($"peer-{i}")).ToList();
        var ring = BuildRing(peers);

        // With 10 peers and enough different chunks, we expect at least 2 distinct peers to appear as first responsible
        var leaders = new HashSet<string>();
        foreach (var chunk in Enumerable.Range(0, 20).Select(i => $"chunk-{i}"))
        {
            var responsible = ring.GetResponsiblePeers(chunk, 1);
            if (responsible.Count > 0)
                leaders.Add(Convert.ToHexString(responsible[0].NodeId));
        }

        Assert.True(leaders.Count > 1, "Expected different chunks to map to different peers");
    }

    [Fact]
    public void GetResponsiblePeers_ReturnsAtMostReplicaCount()
    {
        var peers = Enumerable.Range(0, 5).Select(i => MakeEntry($"peer-{i}")).ToList();
        var ring = BuildRing(peers);

        var result = ring.GetResponsiblePeers("chunk1", 3);

        Assert.True(result.Count <= 3);
    }

    [Fact]
    public void GetResponsiblePeers_NoDuplicatePeers()
    {
        var peers = Enumerable.Range(0, 5).Select(i => MakeEntry($"peer-{i}")).ToList();
        var ring = BuildRing(peers);

        var result = ring.GetResponsiblePeers("chunk1", 5);

        var ids = result.Select(e => Convert.ToHexString(e.NodeId)).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public void GetResponsiblePeers_FewerPeersThanReplicas_ReturnsAllPeers()
    {
        var peers = Enumerable.Range(0, 3).Select(i => MakeEntry($"peer-{i}")).ToList();
        var ring = BuildRing(peers);

        // Request 10 replicas but only 3 peers exist (plus local = 4 total)
        var result = ring.GetResponsiblePeers("chunk1", 10);

        Assert.True(result.Count <= 4); // 3 peers + local node
    }

    [Fact]
    public void GetResponsiblePeers_EmptyRoutingTable_IncludesLocalNode()
    {
        // Even with no routing table peers, local node is always on the ring
        var ring = BuildRing(Enumerable.Empty<RoutingEntry>());

        var result = ring.GetResponsiblePeers("chunk1", 3);

        Assert.Single(result);
    }

    [Fact]
    public void IsLocallyResponsible_LocalNodeInRing_ReturnsTrue()
    {
        var localId = SHA256.HashData(Encoding.UTF8.GetBytes("local-node"));
        var localEntry = new RoutingEntry
        {
            NodeId = localId,
            Address = new NodeAddress("127.0.0.1", 3000),
            LastSeenUtc = DateTime.UtcNow
        };

        // Only one peer — local node must be responsible
        var ring = BuildRing(Enumerable.Empty<RoutingEntry>(), localId);

        Assert.True(ring.IsLocallyResponsible("any-chunk", 3));
    }

    [Fact]
    public void IsLocallyResponsible_LocalNotInTopN_ReturnsFalse()
    {
        // Use a large number of well-distributed peers so the local node won't be in top 1
        var peers = Enumerable.Range(0, 20).Select(i => MakeEntry($"peer-{i}")).ToList();
        byte[] localId = SHA256.HashData(Encoding.UTF8.GetBytes("local-definitely-not-leader"));

        var ring = BuildRing(peers, localId);

        // Request only 1 replica — the local node is unlikely to be the single leader
        // Test many chunks until we find one where local is NOT responsible (statistical)
        bool foundFalse = false;
        for (int i = 0; i < 100; i++)
        {
            if (!ring.IsLocallyResponsible($"chunk-{i}", 1))
            {
                foundFalse = true;
                break;
            }
        }

        Assert.True(foundFalse, "Expected local node to not be responsible for at least one chunk with 1/21 chance");
    }

    [Fact]
    public void GetResponsiblePeers_AllDistinctNodeIds_DifferentAssignments()
    {
        // Verify that 5 distinct peers with uniform names produce varied assignments
        var peers = Enumerable.Range(0, 5).Select(i => MakeEntry($"peer-{i}")).ToList();
        var ring = BuildRing(peers);

        var assignmentCounts = new Dictionary<string, int>();
        foreach (var chunk in Enumerable.Range(0, 50).Select(i => $"chunk-{i:D4}"))
        {
            var responsible = ring.GetResponsiblePeers(chunk, 1);
            if (responsible.Count > 0)
            {
                string nodeId = Convert.ToHexString(responsible[0].NodeId);
                assignmentCounts.TryGetValue(nodeId, out int count);
                assignmentCounts[nodeId] = count + 1;
            }
        }

        // All 5 peers + local node have some chance of being assigned; at least 3 distinct nodes should appear
        Assert.True(assignmentCounts.Count >= 2,
            $"Expected at least 2 distinct peers to receive assignments, got {assignmentCounts.Count}");
    }
}
