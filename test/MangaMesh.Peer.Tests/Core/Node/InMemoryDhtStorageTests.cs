using MangaMesh.Peer.Core.Node;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Node;

public class InMemoryDhtStorageTests
{
    private readonly InMemoryDhtStorage _sut = new();

    [Fact]
    public void StoreContent_NewEntry_CanBeRetrieved()
    {
        var contentHash = new byte[] { 1, 2, 3, 4 };
        var publisherId = new byte[] { 10, 20, 30 };

        _sut.StoreContent(contentHash, publisherId);

        var nodes = _sut.GetNodesForContent(contentHash);
        Assert.Single(nodes);
        Assert.Equal(publisherId, nodes[0]);
    }

    [Fact]
    public void StoreContent_MultiplePublishers_AllReturned()
    {
        var contentHash = new byte[] { 1, 2, 3 };
        var pub1 = new byte[] { 0x01 };
        var pub2 = new byte[] { 0x02 };

        _sut.StoreContent(contentHash, pub1);
        _sut.StoreContent(contentHash, pub2);

        var nodes = _sut.GetNodesForContent(contentHash);
        Assert.Equal(2, nodes.Count);
    }

    [Fact]
    public void StoreContent_SamePublisherTwice_NoDuplicate()
    {
        var contentHash = new byte[] { 0xAA, 0xBB };
        var pub = new byte[] { 0x01 };

        _sut.StoreContent(contentHash, pub);
        _sut.StoreContent(contentHash, pub);

        var nodes = _sut.GetNodesForContent(contentHash);
        Assert.Single(nodes);
    }

    [Fact]
    public void GetNodesForContent_UnknownHash_ReturnsEmpty()
    {
        var unknown = new byte[] { 0xFF, 0xFF };
        var nodes = _sut.GetNodesForContent(unknown);
        Assert.Empty(nodes);
    }

    [Fact]
    public void GetAllContentHashes_EmptyStore_ReturnsEmpty()
    {
        var hashes = _sut.GetAllContentHashes();
        Assert.Empty(hashes);
    }

    [Fact]
    public void GetAllContentHashes_AfterStore_ReturnsHashes()
    {
        var hash1 = new byte[] { 0x01, 0x02 };
        var hash2 = new byte[] { 0x03, 0x04 };

        _sut.StoreContent(hash1, new byte[] { 0xAA });
        _sut.StoreContent(hash2, new byte[] { 0xBB });

        var allHashes = _sut.GetAllContentHashes();
        Assert.Equal(2, allHashes.Count);
        Assert.Contains(allHashes, h => h.SequenceEqual(hash1));
        Assert.Contains(allHashes, h => h.SequenceEqual(hash2));
    }

    [Fact]
    public void StoreContent_DifferentContentHashes_StoredSeparately()
    {
        var hash1 = new byte[] { 0x01 };
        var hash2 = new byte[] { 0x02 };
        var pub = new byte[] { 0xAA };

        _sut.StoreContent(hash1, pub);
        _sut.StoreContent(hash2, pub);

        Assert.Single(_sut.GetNodesForContent(hash1));
        Assert.Single(_sut.GetNodesForContent(hash2));
        Assert.Equal(2, _sut.GetAllContentHashes().Count);
    }
}
