using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Blob;

public class BlobStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IStorageMonitorService> _storageMonitor;
    private readonly BlobStore _sut;

    public BlobStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "BlobStoreTests_" + Guid.NewGuid());
        _storageMonitor = new Mock<IStorageMonitorService>();
        _storageMonitor
            .Setup(s => s.EnsureStorageAvailable(It.IsAny<long>()))
            .Returns(Task.CompletedTask);

        var options = Options.Create(new BlobStoreOptions
        {
            RootPath = _tempDir,
            MaxStorageBytes = 100L * 1024 * 1024
        });

        _sut = new BlobStore(options, _storageMonitor.Object, NullLogger<BlobStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task PutAsync_NewBlob_ReturnsHash()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("hello world");
        using var stream = new MemoryStream(data);

        var hash = await _sut.PutAsync(stream);

        Assert.NotNull(hash.Value);
        Assert.Equal(64, hash.Value.Length); // SHA256 hex string
    }

    [Fact]
    public async Task PutAsync_SameContent_ReturnsSameHash()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("same content");
        using var s1 = new MemoryStream(data);
        using var s2 = new MemoryStream(data);

        var h1 = await _sut.PutAsync(s1);
        var h2 = await _sut.PutAsync(s2);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public async Task PutAsync_ExistingBlob_DoesNotCallEnsureStorage()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("duplicate");
        using var s1 = new MemoryStream(data);
        using var s2 = new MemoryStream(data);

        await _sut.PutAsync(s1);
        // Reset to check second call does not call EnsureStorageAvailable
        _storageMonitor.Invocations.Clear();

        await _sut.PutAsync(s2);

        _storageMonitor.Verify(s => s.EnsureStorageAvailable(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task PutAsync_NewBlob_NotifiesStorageMonitor()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("notify test");
        using var stream = new MemoryStream(data);

        await _sut.PutAsync(stream);

        _storageMonitor.Verify(s => s.NotifyBlobWritten(It.IsAny<long>()), Times.Once);
    }

    [Fact]
    public async Task OpenReadAsync_ExistingBlob_ReturnsStream()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("readable");
        using var putStream = new MemoryStream(data);
        var hash = await _sut.PutAsync(putStream);

        var readStream = await _sut.OpenReadAsync(hash);

        Assert.NotNull(readStream);
        using var reader = new StreamReader(readStream!);
        Assert.Equal("readable", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task OpenReadAsync_MissingBlob_ReturnsNull()
    {
        var fakeHash = new BlobHash("a".PadRight(64, 'b'));
        var result = await _sut.OpenReadAsync(fakeHash);
        Assert.Null(result);
    }

    [Fact]
    public async Task Exists_AfterPut_ReturnsTrue()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("exists");
        using var stream = new MemoryStream(data);
        var hash = await _sut.PutAsync(stream);

        Assert.True(_sut.Exists(hash));
    }

    [Fact]
    public void Exists_UnknownHash_ReturnsFalse()
    {
        var fakeHash = new BlobHash("a".PadRight(64, 'b'));
        Assert.False(_sut.Exists(fakeHash));
    }

    [Fact]
    public async Task GetSize_ExistingBlob_ReturnsSize()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("size-check");
        using var stream = new MemoryStream(data);
        var hash = await _sut.PutAsync(stream);

        var size = _sut.GetSize(hash);
        Assert.Equal(data.Length, size);
    }

    [Fact]
    public void GetSize_UnknownHash_ReturnsZero()
    {
        var fakeHash = new BlobHash("a".PadRight(64, 'b'));
        Assert.Equal(0L, _sut.GetSize(fakeHash));
    }

    [Fact]
    public async Task Delete_ExistingBlob_RemovesIt()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("delete me");
        using var stream = new MemoryStream(data);
        var hash = await _sut.PutAsync(stream);

        _sut.Delete(hash);

        Assert.False(_sut.Exists(hash));
    }

    [Fact]
    public void Delete_NonExistentHash_DoesNotThrow()
    {
        var fakeHash = new BlobHash("a".PadRight(64, 'b'));
        var ex = Record.Exception(() => _sut.Delete(fakeHash));
        Assert.Null(ex);
    }

    [Fact]
    public async Task GetAllHashes_ReturnsStoredHashes()
    {
        var data1 = System.Text.Encoding.UTF8.GetBytes("first");
        var data2 = System.Text.Encoding.UTF8.GetBytes("second");
        using var s1 = new MemoryStream(data1);
        using var s2 = new MemoryStream(data2);
        var h1 = await _sut.PutAsync(s1);
        var h2 = await _sut.PutAsync(s2);

        var hashes = _sut.GetAllHashes().ToList();

        Assert.Contains(h1, hashes);
        Assert.Contains(h2, hashes);
    }

    [Fact]
    public void GetAllHashes_EmptyStore_ReturnsEmpty()
    {
        var hashes = _sut.GetAllHashes().ToList();
        Assert.Empty(hashes);
    }

    [Fact]
    public async Task PutAsync_StorageLimitExceeded_Throws()
    {
        _storageMonitor
            .Setup(s => s.EnsureStorageAvailable(It.IsAny<long>()))
            .ThrowsAsync(new IOException("Storage limit exceeded"));

        var data = System.Text.Encoding.UTF8.GetBytes("overflow");
        using var stream = new MemoryStream(data);

        await Assert.ThrowsAsync<IOException>(() => _sut.PutAsync(stream));
    }
}
