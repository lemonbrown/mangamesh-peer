using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Storage;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Storage;

public class StorageMonitorServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IManifestStore> _manifestStore;
    private readonly StorageMonitorService _sut;

    public StorageMonitorServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "StorageMonitorTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);

        _manifestStore = new Mock<IManifestStore>();
        _manifestStore
            .Setup(m => m.GetAllHashesAsync())
            .ReturnsAsync(Enumerable.Empty<ManifestHash>());

        var options = Options.Create(new BlobStoreOptions
        {
            RootPath = _tempDir,
            MaxStorageBytes = 100L * 1024 * 1024
        });

        _sut = new StorageMonitorService(options, _manifestStore.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task GetStorageStatsAsync_EmptyDir_ReturnsZeroUsed()
    {
        var stats = await _sut.GetStorageStatsAsync();

        Assert.Equal(0, stats.UsedMb, precision: 2);
        Assert.Equal(0, stats.ManifestCount);
    }

    [Fact]
    public async Task GetStorageStatsAsync_WithManifests_ReturnsCount()
    {
        _manifestStore
            .Setup(m => m.GetAllHashesAsync())
            .ReturnsAsync(new[] { new ManifestHash("abc"), new ManifestHash("def") });

        var stats = await _sut.GetStorageStatsAsync();

        Assert.Equal(2, stats.ManifestCount);
    }

    [Fact]
    public async Task GetStorageStatsAsync_TotalMbMatchesConfig()
    {
        var stats = await _sut.GetStorageStatsAsync();

        // 100MB configured
        Assert.Equal(100.0, stats.TotalMb, precision: 1);
    }

    [Fact]
    public async Task GetStorageStatsAsync_WithFiles_ReportsUsedBytes()
    {
        var filePath = Path.Combine(_tempDir, "test.dat");
        await File.WriteAllBytesAsync(filePath, new byte[1024 * 1024]); // 1 MB

        var stats = await _sut.GetStorageStatsAsync();

        Assert.True(stats.UsedMb >= 1.0);
    }

    [Fact]
    public async Task EnsureStorageAvailable_WithinLimit_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(() => _sut.EnsureStorageAvailable(1024));
        Assert.Null(ex);
    }

    [Fact]
    public async Task EnsureStorageAvailable_ExceedsLimit_ThrowsIOException()
    {
        // Require more than 100MB limit
        await Assert.ThrowsAsync<IOException>(() =>
            _sut.EnsureStorageAvailable(200L * 1024 * 1024));
    }

    [Fact]
    public async Task NotifyBlobWritten_UpdatesCachedUsedBytes()
    {
        // Force a read so cache is populated
        await _sut.EnsureStorageAvailable(0);

        // Should not throw
        _sut.NotifyBlobWritten(1024);
    }

    [Fact]
    public async Task EnsureStorageAvailable_AfterNotifyBlobWritten_AccountsForNewData()
    {
        // Fill cache
        await _sut.EnsureStorageAvailable(0);

        // Add 99 MB worth of "written" data
        _sut.NotifyBlobWritten(99L * 1024 * 1024);

        // Requesting 2 more MB should exceed the 100MB limit
        await Assert.ThrowsAsync<IOException>(() =>
            _sut.EnsureStorageAvailable(2L * 1024 * 1024));
    }
}
