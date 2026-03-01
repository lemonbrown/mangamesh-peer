using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Shared.Models;
using Moq;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Chapters;

public class ChapterIngestionServiceTests
{
    private readonly Mock<IChapterSourceReader> _reader;
    private readonly Mock<IImageFormatProvider> _imageFormats;
    private readonly Mock<IChunkIngester> _chunkIngester;
    private readonly ChapterIngestionService _sut;

    public ChapterIngestionServiceTests()
    {
        _reader = new Mock<IChapterSourceReader>();
        _imageFormats = new Mock<IImageFormatProvider>();
        _chunkIngester = new Mock<IChunkIngester>();

        _reader.Setup(r => r.CanRead(It.IsAny<string>())).Returns(true);
        _imageFormats.Setup(f => f.GetMimeType(It.IsAny<string>())).Returns("image/jpeg");

        _sut = new ChapterIngestionService(
            new[] { _reader.Object },
            _imageFormats.Object,
            _chunkIngester.Object);
    }

    private static async IAsyncEnumerable<(string name, Stream content)> SingleFile(string name, byte[] data)
    {
        yield return (name, new MemoryStream(data));
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<(string name, Stream content)> MultipleFiles(
        IEnumerable<(string name, byte[] data)> files)
    {
        foreach (var (name, data) in files)
        {
            yield return (name, new MemoryStream(data));
        }
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<(string name, Stream content)> EmptyFiles()
    {
        await Task.CompletedTask;
        yield break;
    }

    [Fact]
    public async Task IngestDirectoryAsync_NoReaderCanRead_ThrowsDirectoryNotFoundException()
    {
        var noReader = new Mock<IChapterSourceReader>();
        noReader.Setup(r => r.CanRead(It.IsAny<string>())).Returns(false);

        var sut = new ChapterIngestionService(
            new[] { noReader.Object },
            _imageFormats.Object,
            _chunkIngester.Object);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => sut.IngestDirectoryAsync("/nonexistent"));
    }

    [Fact]
    public async Task IngestDirectoryAsync_SingleFile_ReturnsOneEntry()
    {
        var data = new byte[] { 1, 2, 3 };
        _reader
            .Setup(r => r.ReadFilesAsync("/path", default))
            .Returns(SingleFile("page1.jpg", data));

        var pageManifest = new PageManifest
        {
            FileSize = data.Length,
            Chunks = new List<string> { "aabbccdd" }
        };
        _chunkIngester
            .Setup(c => c.IngestAsync(It.IsAny<Stream>(), "image/jpeg"))
            .ReturnsAsync((pageManifest, "manifestHash1"));

        var (entries, totalSize) = await _sut.IngestDirectoryAsync("/path");

        Assert.Single(entries);
        Assert.Equal("page1.jpg", entries[0].Path);
        Assert.Equal("manifestHash1", entries[0].Hash);
        Assert.Equal(data.Length, totalSize);
    }

    [Fact]
    public async Task IngestDirectoryAsync_MultipleFiles_ReturnsAllEntries()
    {
        var files = new[]
        {
            ("page1.jpg", new byte[] { 1, 2 }),
            ("page2.jpg", new byte[] { 3, 4, 5 })
        };

        _reader
            .Setup(r => r.ReadFilesAsync("/path", default))
            .Returns(MultipleFiles(files));

        var manifest1 = new PageManifest { FileSize = 2, Chunks = new List<string>() };
        var manifest2 = new PageManifest { FileSize = 3, Chunks = new List<string>() };

        _chunkIngester
            .SetupSequence(c => c.IngestAsync(It.IsAny<Stream>(), "image/jpeg"))
            .ReturnsAsync((manifest1, "hash1"))
            .ReturnsAsync((manifest2, "hash2"));

        var (entries, totalSize) = await _sut.IngestDirectoryAsync("/path");

        Assert.Equal(2, entries.Count);
        Assert.Equal(5L, totalSize);
    }

    [Fact]
    public async Task IngestDirectoryAsync_EmptySource_ReturnsEmptyEntries()
    {
        _reader
            .Setup(r => r.ReadFilesAsync("/path", default))
            .Returns(EmptyFiles());

        var (entries, totalSize) = await _sut.IngestDirectoryAsync("/path");

        Assert.Empty(entries);
        Assert.Equal(0L, totalSize);
    }

    [Fact]
    public async Task IngestDirectoryAsync_WithDhtNode_AnnouncesToDht()
    {
        var mockDht = new Mock<IDhtNode>();
        mockDht
            .Setup(d => d.StoreAsync(It.IsAny<byte[]>()))
            .Returns(Task.CompletedTask);

        var sut = new ChapterIngestionService(
            new[] { _reader.Object },
            _imageFormats.Object,
            _chunkIngester.Object,
            mockDht.Object);

        var data = new byte[] { 1, 2, 3 };
        _reader
            .Setup(r => r.ReadFilesAsync("/path", default))
            .Returns(SingleFile("page1.jpg", data));

        var chunkHash = new string('a', 64); // valid hex hash
        var pageManifest = new PageManifest
        {
            FileSize = data.Length,
            Chunks = new List<string> { chunkHash }
        };

        var pageHash = new string('b', 64);
        _chunkIngester
            .Setup(c => c.IngestAsync(It.IsAny<Stream>(), "image/jpeg"))
            .ReturnsAsync((pageManifest, pageHash));

        await sut.IngestDirectoryAsync("/path");

        // Should call StoreAsync for page hash + each chunk hash
        mockDht.Verify(d => d.StoreAsync(It.IsAny<byte[]>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task IngestDirectoryAsync_UsesCorrectMimeType()
    {
        _imageFormats.Setup(f => f.GetMimeType("image.png")).Returns("image/png");

        _reader
            .Setup(r => r.ReadFilesAsync("/path", default))
            .Returns(SingleFile("image.png", new byte[] { 1 }));

        var pageManifest = new PageManifest { FileSize = 1, Chunks = new List<string>() };
        _chunkIngester
            .Setup(c => c.IngestAsync(It.IsAny<Stream>(), "image/png"))
            .ReturnsAsync((pageManifest, "h"));

        await _sut.IngestDirectoryAsync("/path");

        _chunkIngester.Verify(c => c.IngestAsync(It.IsAny<Stream>(), "image/png"), Times.Once);
    }
}
