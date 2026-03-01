using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using Moq;
using NSec.Cryptography;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Chapters;

public class ChapterPublisherServiceTests
{
    private readonly Mock<IKeyStore> _keyStore;
    private readonly Mock<INodeIdentity> _nodeIdentity;
    private readonly Mock<IManifestStore> _manifestStore;
    private readonly Mock<IManifestSigningService> _manifestSigning;
    private readonly Mock<ITrackerPublisher> _trackerPublisher;
    private readonly ChapterPublisherService _sut;

    private readonly string _testPublicKey;
    private readonly string _testPrivateKey;

    public ChapterPublisherServiceTests()
    {
        // Generate real Ed25519 key pair for testing
        var creationParameters = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport
        };
        using var key = new Key(SignatureAlgorithm.Ed25519, creationParameters);
        _testPrivateKey = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPrivateKey));
        _testPublicKey = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPublicKey));

        _keyStore = new Mock<IKeyStore>();
        _keyStore
            .Setup(k => k.GetAsync())
            .ReturnsAsync(new PublicPrivateKeyPair
            {
                PublicKeyBase64 = _testPublicKey,
                PrivateKeyBase64 = _testPrivateKey
            });

        _nodeIdentity = new Mock<INodeIdentity>();
        _nodeIdentity
            .Setup(n => n.NodeId)
            .Returns(new byte[32]);

        _manifestStore = new Mock<IManifestStore>();
        _manifestStore
            .Setup(m => m.ExistsAsync(It.IsAny<ManifestHash>()))
            .ReturnsAsync(false);
        _manifestStore
            .Setup(m => m.SaveAsync(It.IsAny<ManifestHash>(), It.IsAny<ChapterManifest>()))
            .Returns(Task.CompletedTask);

        _manifestSigning = new Mock<IManifestSigningService>();
        _manifestSigning
            .Setup(s => s.SignManifest(It.IsAny<ChapterManifest>(), It.IsAny<Key>()))
            .Returns(new SignedChapterManifest
            {
                Signature = "test-signature",
                PublisherPublicKey = _testPublicKey
            });

        _trackerPublisher = new Mock<ITrackerPublisher>();
        _trackerPublisher
            .Setup(p => p.PublishManifestAsync(It.IsAny<Shared.Models.AnnounceManifestRequest>(), default))
            .Returns(Task.CompletedTask);

        _sut = new ChapterPublisherService(
            _keyStore.Object,
            _nodeIdentity.Object,
            _manifestStore.Object,
            _manifestSigning.Object,
            _trackerPublisher.Object);
    }

    private ImportChapterRequest BuildRequest() => new ImportChapterRequest
    {
        SeriesId = "series-1",
        ScanlatorId = "tcb",
        Language = "en",
        ChapterNumber = 1.0,
        DisplayName = "Chapter 1",
        ExternalMangaId = "ext-1",
        Source = ExternalMetadataSource.MangaDex,
        ReleaseType = ReleaseType.VerifiedScanlation
    };

    [Fact]
    public async Task PublishChapterAsync_NewManifest_ReturnsHashAndNotAlreadyExists()
    {
        var request = BuildRequest();
        var entries = new List<ChapterFileEntry>();

        var (hash, alreadyExists) = await _sut.PublishChapterAsync(
            request, "series-1", "One Piece", entries, 0L);

        Assert.NotNull(hash.Value);
        Assert.False(alreadyExists);
    }

    [Fact]
    public async Task PublishChapterAsync_ExistingManifest_ReturnsAlreadyExists()
    {
        _manifestStore
            .Setup(m => m.ExistsAsync(It.IsAny<ManifestHash>()))
            .ReturnsAsync(true);

        var request = BuildRequest();

        var (_, alreadyExists) = await _sut.PublishChapterAsync(
            request, "series-1", "Title", new List<ChapterFileEntry>(), 0L);

        Assert.True(alreadyExists);
    }

    [Fact]
    public async Task PublishChapterAsync_NewManifest_SavesManifestTwice()
    {
        var request = BuildRequest();

        await _sut.PublishChapterAsync(request, "series-1", "Title", new List<ChapterFileEntry>(), 0L);

        // Save is called twice: once unsigned, once with signature
        _manifestStore.Verify(m => m.SaveAsync(It.IsAny<ManifestHash>(), It.IsAny<ChapterManifest>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PublishChapterAsync_NewManifest_PublishesToTracker()
    {
        var request = BuildRequest();

        await _sut.PublishChapterAsync(request, "series-1", "Title", new List<ChapterFileEntry>(), 0L);

        _trackerPublisher.Verify(
            p => p.PublishManifestAsync(It.IsAny<Shared.Models.AnnounceManifestRequest>(), default),
            Times.Once);
    }

    [Fact]
    public async Task PublishChapterAsync_ExistingManifest_DoesNotPublishToTracker()
    {
        _manifestStore
            .Setup(m => m.ExistsAsync(It.IsAny<ManifestHash>()))
            .ReturnsAsync(true);

        await _sut.PublishChapterAsync(BuildRequest(), "s", "T", new List<ChapterFileEntry>(), 0L);

        _trackerPublisher.Verify(
            p => p.PublishManifestAsync(It.IsAny<Shared.Models.AnnounceManifestRequest>(), default),
            Times.Never);
    }

    [Fact]
    public async Task PublishChapterAsync_TitleContainsExternalMangaId_ReplacesWithSeriesTitle()
    {
        var request = new ImportChapterRequest
        {
            SeriesId = "series-1",
            ScanlatorId = "tcb",
            Language = "en",
            ChapterNumber = 1,
            DisplayName = "ext-1 Chapter 1",
            ExternalMangaId = "ext-1",
            Source = ExternalMetadataSource.MangaDex,
            ReleaseType = ReleaseType.VerifiedScanlation
        };

        ChapterManifest? savedManifest = null;
        _manifestStore
            .Setup(m => m.SaveAsync(It.IsAny<ManifestHash>(), It.IsAny<ChapterManifest>()))
            .Callback<ManifestHash, ChapterManifest>((_, m) => savedManifest = m)
            .Returns(Task.CompletedTask);

        await _sut.PublishChapterAsync(request, "series-1", "One Piece", new List<ChapterFileEntry>(), 0L);

        Assert.NotNull(savedManifest);
        Assert.Contains("One Piece", savedManifest!.Title);
        Assert.DoesNotContain("ext-1", savedManifest.Title);
    }

    [Fact]
    public async Task PublishChapterAsync_WithDhtNode_StoresManifestHash()
    {
        var mockDht = new Mock<IDhtNode>();
        mockDht.Setup(d => d.StoreAsync(It.IsAny<byte[]>())).Returns(Task.CompletedTask);

        var sut = new ChapterPublisherService(
            _keyStore.Object,
            _nodeIdentity.Object,
            _manifestStore.Object,
            _manifestSigning.Object,
            _trackerPublisher.Object,
            mockDht.Object);

        await sut.PublishChapterAsync(BuildRequest(), "s", "T", new List<ChapterFileEntry>(), 0L);

        mockDht.Verify(d => d.StoreAsync(It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public async Task ReannounceAsync_ManifestNotFound_ThrowsFileNotFoundException()
    {
        _manifestStore
            .Setup(m => m.GetAsync(It.IsAny<ManifestHash>()))
            .ReturnsAsync((ChapterManifest?)null);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _sut.ReannounceAsync(new ManifestHash("missing"), "node-id"));
    }

    [Fact]
    public async Task ReannounceAsync_ManifestMissingSignature_ThrowsInvalidOperationException()
    {
        var manifest = new ChapterManifest
        {
            ChapterId = "id",
            SeriesId = "s",
            Title = "t",
            Language = "en",
            ScanGroup = "sg",
            Signature = "",
            PublicKey = _testPublicKey
        };

        _manifestStore
            .Setup(m => m.GetAsync(It.IsAny<ManifestHash>()))
            .ReturnsAsync(manifest);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ReannounceAsync(new ManifestHash("hash"), "node-id"));
    }

    [Fact]
    public async Task ReannounceAsync_ValidManifest_PublishesToTracker()
    {
        var manifest = new ChapterManifest
        {
            ChapterId = "id",
            SeriesId = "s",
            Title = "t",
            Language = "en",
            ScanGroup = "sg",
            Signature = "sig",
            PublicKey = _testPublicKey,
            Files = new List<ChapterFileEntry>()
        };

        _manifestStore
            .Setup(m => m.GetAsync(It.IsAny<ManifestHash>()))
            .ReturnsAsync(manifest);

        await _sut.ReannounceAsync(new ManifestHash("hash"), "node-id");

        _trackerPublisher.Verify(
            p => p.PublishManifestAsync(It.IsAny<Shared.Models.AnnounceManifestRequest>(), default),
            Times.Once);
    }
}
