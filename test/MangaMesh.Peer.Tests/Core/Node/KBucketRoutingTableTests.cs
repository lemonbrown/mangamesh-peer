using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Transport;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Node;

public class KBucketRoutingTableTests
{
    private static byte[] NodeId(byte b) => Enumerable.Repeat(b, 32).ToArray();

    private static RoutingEntry Entry(byte[] nodeId, string address = "127.0.0.1", int port = 1234) =>
        new RoutingEntry
        {
            NodeId = nodeId,
            Address = new NodeAddress(address, port)
        };

    [Fact]
    public void Constructor_CreatesBucketsCorrectly()
    {
        var localId = NodeId(0x00);
        var table = new KBucketRoutingTable(localId, 256);
        Assert.Equal(256, table.BucketCount);
    }

    [Fact]
    public void AddOrUpdate_SingleEntry_IsStored()
    {
        var localId = NodeId(0x00);
        var table = new KBucketRoutingTable(localId);
        var peerId = NodeId(0xFF);

        table.AddOrUpdate(Entry(peerId));

        Assert.Contains(table.GetAll(), e => e.NodeId.SequenceEqual(peerId));
    }

    [Fact]
    public void AddOrUpdate_SelfEntry_IsIgnored()
    {
        var localId = NodeId(0x01);
        var table = new KBucketRoutingTable(localId);

        table.AddOrUpdate(Entry(localId));

        Assert.Empty(table.GetAll());
    }

    [Fact]
    public void AddOrUpdate_NullNodeId_IsIgnored()
    {
        var localId = NodeId(0x01);
        var table = new KBucketRoutingTable(localId);

        table.AddOrUpdate(new RoutingEntry { NodeId = null!, Address = new NodeAddress("127.0.0.1", 0) });

        Assert.Empty(table.GetAll());
    }

    [Fact]
    public void AddOrUpdate_MismatchedLengthNodeId_IsIgnored()
    {
        var localId = NodeId(0x01);
        var table = new KBucketRoutingTable(localId);

        table.AddOrUpdate(new RoutingEntry { NodeId = new byte[] { 0xFF }, Address = new NodeAddress("127.0.0.1", 0) });

        Assert.Empty(table.GetAll());
    }

    [Fact]
    public void AddOrUpdate_MultipleDistinctPeers_AllStored()
    {
        var localId = NodeId(0x00);
        var table = new KBucketRoutingTable(localId);

        table.AddOrUpdate(Entry(NodeId(0x01)));
        table.AddOrUpdate(Entry(NodeId(0x02)));
        table.AddOrUpdate(Entry(NodeId(0x03)));

        Assert.Equal(3, table.GetAll().Count);
    }

    [Fact]
    public void FindClosest_ReturnsKClosestNodes()
    {
        var localId = NodeId(0x00);
        var table = new KBucketRoutingTable(localId);

        for (byte i = 1; i <= 30; i++)
            table.AddOrUpdate(Entry(NodeId(i)));

        var target = NodeId(0x05);
        var closest = table.FindClosest(target, k: 5);

        Assert.Equal(5, closest.Count);
    }

    [Fact]
    public void FindClosest_FewerThanKNodes_ReturnsAll()
    {
        var localId = NodeId(0x00);
        var table = new KBucketRoutingTable(localId);

        table.AddOrUpdate(Entry(NodeId(0x01)));
        table.AddOrUpdate(Entry(NodeId(0x02)));

        var closest = table.FindClosest(NodeId(0x01), k: 20);

        Assert.Equal(2, closest.Count);
    }

    [Fact]
    public void FindClosest_EmptyTable_ReturnsEmpty()
    {
        var localId = NodeId(0x00);
        var table = new KBucketRoutingTable(localId);

        var closest = table.FindClosest(NodeId(0xFF));

        Assert.Empty(closest);
    }

    [Fact]
    public void GetAddressForNode_KnownNode_ReturnsAddress()
    {
        var localId = NodeId(0x00);
        var table = new KBucketRoutingTable(localId);
        var peerId = NodeId(0xAA);
        var addr = new NodeAddress("10.0.0.1", 9000);

        table.AddOrUpdate(new RoutingEntry { NodeId = peerId, Address = addr });

        var found = table.GetAddressForNode(peerId);
        Assert.NotNull(found);
        Assert.Equal("10.0.0.1", found!.Host);
        Assert.Equal(9000, found.Port);
    }

    [Fact]
    public void GetAddressForNode_UnknownNode_ReturnsNull()
    {
        var localId = NodeId(0x00);
        var table = new KBucketRoutingTable(localId);

        var result = table.GetAddressForNode(NodeId(0xFF));

        Assert.Null(result);
    }

    [Fact]
    public void GetAll_EmptyTable_ReturnsEmpty()
    {
        var table = new KBucketRoutingTable(NodeId(0x00));
        Assert.Empty(table.GetAll());
    }

    [Fact]
    public void AddOrUpdate_DuplicatePeer_UpdatesExisting()
    {
        var localId = NodeId(0x00);
        var table = new KBucketRoutingTable(localId);
        var peerId = NodeId(0x10);

        table.AddOrUpdate(Entry(peerId, "10.0.0.1", 1000));
        table.AddOrUpdate(Entry(peerId, "10.0.0.2", 2000));

        // Should still have 1 entry, updated
        var all = table.GetAll();
        Assert.Single(all);
    }
}
