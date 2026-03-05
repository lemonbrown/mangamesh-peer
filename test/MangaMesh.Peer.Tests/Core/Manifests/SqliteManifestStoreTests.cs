using MangaMesh.Peer.Core.Data;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Manifests;

public class SqliteManifestStoreTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly SqliteManifestStore _sut;
    private readonly ClientDbContext _dbContext;

    public SqliteManifestStoreTests()
    {
        var services = new ServiceCollection();

        // Use a unique in-memory SQLite database for each test instance
        var dbId = Guid.NewGuid().ToString();
        services.AddDbContext<ClientDbContext>(options =>
            options.UseSqlite($"DataSource=file:{dbId}?mode=memory&cache=shared"));

        _serviceProvider = services.BuildServiceProvider();

        _dbContext = _serviceProvider.GetRequiredService<ClientDbContext>();
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();

        // Ensure Manifests table schema is created for testing
        _dbContext.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""Manifests"" (
                ""Hash"" TEXT NOT NULL CONSTRAINT ""PK_Manifests"" PRIMARY KEY,
                ""SeriesId"" TEXT NOT NULL,
                ""ChapterId"" TEXT NOT NULL,
                ""DataJson"" TEXT NOT NULL,
                ""CreatedUtc"" TEXT NOT NULL,
                ""IsDownloaded"" INTEGER NOT NULL DEFAULT 0
            );
        ");

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _sut = new SqliteManifestStore(scopeFactory);
    }

    public void Dispose()
    {
        _dbContext.Database.CloseConnection();
        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task PutAsync_SavesManifest_And_MarkAsDownloadedAsync_UpdatesFlag()
    {
        // Arrange
        var manifest = new ChapterManifest
        {
            SeriesId = "series-1",
            ChapterId = "ch-1",
            SchemaVersion = 1,
            Volume = "1",
            Language = "en",
            ScanGroup = "Test Group",
            Title = "Chapter 1",
            Files = new List<ChapterFileEntry>
            {
                new ChapterFileEntry { Hash = "hash1", Path = "01.jpg", Size = 1024 }
            },
            Quality = "High",
            CreatedUtc = DateTime.UtcNow
        };

        // Act - Put manifest
        var hash = await _sut.PutAsync(manifest);

        // Assert - Initial state is not downloaded
        var allWithData = await _sut.GetAllWithDataAsync();
        Assert.Single(allWithData);
        var entry = allWithData[0];
        Assert.Equal(hash.Value, entry.Hash.Value);
        Assert.False(entry.IsDownloaded, "Newly saved manifest should not be marked as downloaded.");

        // Act - Mark as downloaded
        await _sut.MarkAsDownloadedAsync(hash);

        // Assert - State is updated
        allWithData = await _sut.GetAllWithDataAsync();
        Assert.Single(allWithData);
        entry = allWithData[0];
        Assert.True(entry.IsDownloaded, "Manifest should be marked as downloaded after calling MarkAsDownloadedAsync.");
    }
}
