using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Chapters;

public class ImportChapterServiceTests
{
    private readonly Mock<IChapterIngestionService> _ingestion;
    private readonly Mock<ISeriesRegistry> _seriesRegistry;
    private readonly Mock<IChapterPublisherService> _publisher;
    private readonly ImportChapterService _sut;

    public ImportChapterServiceTests()
    {
        _ingestion = new Mock<IChapterIngestionService>();
        _seriesRegistry = new Mock<ISeriesRegistry>();
        _publisher = new Mock<IChapterPublisherService>();

        _sut = new ImportChapterService(
            _ingestion.Object,
            _seriesRegistry.Object,
            _publisher.Object,
            NullLogger<ImportChapterService>.Instance);
    }

    private ImportChapterRequest BuildRequest() => new ImportChapterRequest
    {
        SeriesId = "series-1",
        ScanlatorId = "tcb",
        Language = "en",
        ChapterNumber = 1.0,
        SourceDirectory = "/tmp/chapter",
        DisplayName = "Chapter 1",
        ExternalMangaId = "ext-123",
        Source = ExternalMetadataSource.MangaDex
    };

    [Fact]
    public async Task ImportAsync_Success_ReturnsResult()
    {
        var request = BuildRequest();
        var entries = new List<ChapterFileEntry>
        {
            new ChapterFileEntry { Path = "page1.jpg", Hash = "abc", Size = 100 },
            new ChapterFileEntry { Path = "page2.jpg", Hash = "def", Size = 200 }
        };
        var hash = new ManifestHash("deadbeef");

        _ingestion
            .Setup(i => i.IngestDirectoryAsync(request.SourceDirectory, default))
            .ReturnsAsync((entries, 300L));

        _seriesRegistry
            .Setup(r => r.RegisterSeriesAsync(request.Source, request.ExternalMangaId))
            .ReturnsAsync(("series-1", "One Piece"));

        _publisher
            .Setup(p => p.PublishChapterAsync(request, "series-1", "One Piece", entries, 300L, default))
            .ReturnsAsync((hash, false));

        var result = await _sut.ImportAsync(request);

        Assert.Equal("deadbeef", result.ManifestHash.Value);
        Assert.Equal(2, result.FileCount);
        Assert.False(result.AlreadyExists);
    }

    [Fact]
    public async Task ImportAsync_AlreadyExists_ThrowsInvalidOperationException()
    {
        var request = BuildRequest();
        var entries = new List<ChapterFileEntry>();
        var hash = new ManifestHash("existinghash");

        _ingestion
            .Setup(i => i.IngestDirectoryAsync(It.IsAny<string>(), default))
            .ReturnsAsync((entries, 0L));

        _seriesRegistry
            .Setup(r => r.RegisterSeriesAsync(It.IsAny<ExternalMetadataSource>(), It.IsAny<string>()))
            .ReturnsAsync(("series-1", "Title"));

        _publisher
            .Setup(p => p.PublishChapterAsync(It.IsAny<ImportChapterRequest>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<ChapterFileEntry>>(), It.IsAny<long>(), default))
            .ReturnsAsync((hash, true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ImportAsync(request));
    }

    [Fact]
    public async Task ImportAsync_CallsIngestionWithCorrectDirectory()
    {
        var request = BuildRequest();

        _ingestion
            .Setup(i => i.IngestDirectoryAsync("/tmp/chapter", default))
            .ReturnsAsync((new List<ChapterFileEntry>(), 0L));

        _seriesRegistry
            .Setup(r => r.RegisterSeriesAsync(It.IsAny<ExternalMetadataSource>(), It.IsAny<string>()))
            .ReturnsAsync(("series-1", ""));

        _publisher
            .Setup(p => p.PublishChapterAsync(It.IsAny<ImportChapterRequest>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<ChapterFileEntry>>(), It.IsAny<long>(), default))
            .ReturnsAsync((new ManifestHash("h"), false));

        await _sut.ImportAsync(request);

        _ingestion.Verify(i => i.IngestDirectoryAsync("/tmp/chapter", default), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_CallsSeriesRegistryWithCorrectArgs()
    {
        var request = BuildRequest();

        _ingestion
            .Setup(i => i.IngestDirectoryAsync(It.IsAny<string>(), default))
            .ReturnsAsync((new List<ChapterFileEntry>(), 0L));

        _seriesRegistry
            .Setup(r => r.RegisterSeriesAsync(ExternalMetadataSource.MangaDex, "ext-123"))
            .ReturnsAsync(("s-1", "Title"));

        _publisher
            .Setup(p => p.PublishChapterAsync(It.IsAny<ImportChapterRequest>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<ChapterFileEntry>>(), It.IsAny<long>(), default))
            .ReturnsAsync((new ManifestHash("h"), false));

        await _sut.ImportAsync(request);

        _seriesRegistry.Verify(r => r.RegisterSeriesAsync(ExternalMetadataSource.MangaDex, "ext-123"), Times.Once);
    }

    [Fact]
    public async Task ReannounceAsync_DelegatesToPublisher()
    {
        var hash = new ManifestHash("testhash");
        var nodeId = "node-abc";

        _publisher
            .Setup(p => p.ReannounceAsync(hash, nodeId))
            .Returns(Task.CompletedTask);

        await _sut.ReannounceAsync(hash, nodeId);

        _publisher.Verify(p => p.ReannounceAsync(hash, nodeId), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_EmptyEntries_ReturnsZeroFileCount()
    {
        var request = BuildRequest();

        _ingestion
            .Setup(i => i.IngestDirectoryAsync(It.IsAny<string>(), default))
            .ReturnsAsync((new List<ChapterFileEntry>(), 0L));

        _seriesRegistry
            .Setup(r => r.RegisterSeriesAsync(It.IsAny<ExternalMetadataSource>(), It.IsAny<string>()))
            .ReturnsAsync(("s", "T"));

        _publisher
            .Setup(p => p.PublishChapterAsync(It.IsAny<ImportChapterRequest>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<ChapterFileEntry>>(), It.IsAny<long>(), default))
            .ReturnsAsync((new ManifestHash("h"), false));

        var result = await _sut.ImportAsync(request);

        Assert.Equal(0, result.FileCount);
    }
}
