using MangaMesh.Peer.ClientApi.Models;
using MangaMesh.Peer.ClientApi.Services;
using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MangaMesh.Peer.Tests.ClientApi.Services;

public class ImportChapterServiceWrapperTests
{
    private readonly Mock<IChapterIngestionService> _ingestion;
    private readonly Mock<ISeriesRegistry> _seriesRegistry;
    private readonly Mock<IChapterPublisherService> _publisher;
    private readonly ImportChapterServiceWrapper _sut;

    public ImportChapterServiceWrapperTests()
    {
        _ingestion = new Mock<IChapterIngestionService>();
        _seriesRegistry = new Mock<ISeriesRegistry>();
        _publisher = new Mock<IChapterPublisherService>();

        var coreService = new ImportChapterService(
            _ingestion.Object,
            _seriesRegistry.Object,
            _publisher.Object,
            NullLogger<ImportChapterService>.Instance);

        _sut = new ImportChapterServiceWrapper(coreService);
    }

    private ImportChapterRequestDto BuildDto(string releaseType = "VerifiedScanlation") =>
        new ImportChapterRequestDto(
            SeriesId: "series-1",
            ScanlatorId: "tcb",
            Language: "en",
            ChapterNumber: 1.0,
            SourcePath: "/tmp/chapter",
            DisplayName: "Chapter 1",
            ReleaseType: releaseType,
            Source: ExternalMetadataSource.MangaDex,
            ExternalMangaId: "ext-1",
            Quality: "Unknown"
        );

    private void SetupSuccessfulImport(ManifestHash hash, int fileCount = 2)
    {
        var entries = Enumerable.Range(0, fileCount)
            .Select(i => new ChapterFileEntry { Path = $"page{i}.jpg", Hash = $"hash{i}", Size = 100 })
            .ToList();

        _ingestion
            .Setup(i => i.IngestDirectoryAsync(It.IsAny<string>(), default))
            .ReturnsAsync((entries, (long)(fileCount * 100)));

        _seriesRegistry
            .Setup(r => r.RegisterSeriesAsync(It.IsAny<ExternalMetadataSource>(), It.IsAny<string>()))
            .ReturnsAsync(("series-1", "One Piece"));

        _publisher
            .Setup(p => p.PublishChapterAsync(It.IsAny<ImportChapterRequest>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<ChapterFileEntry>>(), It.IsAny<long>(), default))
            .ReturnsAsync((hash, false));
    }

    [Fact]
    public async Task ImportAsync_VerifiedScanlation_MapsReleaseType()
    {
        var dto = BuildDto("VerifiedScanlation");
        SetupSuccessfulImport(new ManifestHash("hash1"), 1);

        await _sut.ImportAsync(dto);

        _publisher.Verify(p => p.PublishChapterAsync(
            It.Is<ImportChapterRequest>(r => r.ReleaseType == ReleaseType.VerifiedScanlation),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<ChapterFileEntry>>(), It.IsAny<long>(), default), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_UnverifiedScanlation_MapsReleaseType()
    {
        var dto = BuildDto("UnverifiedScanlation");
        SetupSuccessfulImport(new ManifestHash("hash1"), 1);

        await _sut.ImportAsync(dto);

        _publisher.Verify(p => p.PublishChapterAsync(
            It.Is<ImportChapterRequest>(r => r.ReleaseType == ReleaseType.UnverifiedScanlation),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<ChapterFileEntry>>(), It.IsAny<long>(), default), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_RoughTranslation_MapsReleaseType()
    {
        var dto = BuildDto("RoughTranslation");
        SetupSuccessfulImport(new ManifestHash("hash1"), 1);

        await _sut.ImportAsync(dto);

        _publisher.Verify(p => p.PublishChapterAsync(
            It.Is<ImportChapterRequest>(r => r.ReleaseType == ReleaseType.RoughTranslation),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<ChapterFileEntry>>(), It.IsAny<long>(), default), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_Raw_MapsReleaseType()
    {
        var dto = BuildDto("Raw");
        SetupSuccessfulImport(new ManifestHash("hash1"), 1);

        await _sut.ImportAsync(dto);

        _publisher.Verify(p => p.PublishChapterAsync(
            It.Is<ImportChapterRequest>(r => r.ReleaseType == ReleaseType.Raw),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<ChapterFileEntry>>(), It.IsAny<long>(), default), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_UnknownReleaseType_MapsToUnknown()
    {
        var dto = BuildDto("SomethingElse");
        SetupSuccessfulImport(new ManifestHash("hash1"), 1);

        await _sut.ImportAsync(dto);

        _publisher.Verify(p => p.PublishChapterAsync(
            It.Is<ImportChapterRequest>(r => r.ReleaseType == ReleaseType.Unknown),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<ChapterFileEntry>>(), It.IsAny<long>(), default), Times.Once);
    }

    [Fact]
    public async Task ImportAsync_Success_ReturnsImportResultDto()
    {
        var hash = new ManifestHash("testhash");
        SetupSuccessfulImport(hash, fileCount: 3);

        var result = await _sut.ImportAsync(BuildDto());

        Assert.Equal("testhash", result.ManifestHash);
        Assert.Equal(3, result.FilesImported);
    }

    [Fact]
    public async Task ImportAsync_MapsSourcePathToSourceDirectory()
    {
        SetupSuccessfulImport(new ManifestHash("h"), 1);

        await _sut.ImportAsync(BuildDto());

        _ingestion.Verify(i => i.IngestDirectoryAsync("/tmp/chapter", default), Times.Once);
    }

    [Fact]
    public async Task ReannounceAsync_DelegatesToCoreService()
    {
        var hash = new ManifestHash("testHash");
        _publisher
            .Setup(p => p.ReannounceAsync(hash, "nodeId"))
            .Returns(Task.CompletedTask);

        await _sut.ReannounceAsync(hash, "nodeId");

        _publisher.Verify(p => p.ReannounceAsync(hash, "nodeId"), Times.Once);
    }
}
