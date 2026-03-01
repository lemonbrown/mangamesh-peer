using MangaMesh.Shared.Models;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Models;

public class ManifestHashTests
{
    [Fact]
    public void Parse_ValidValue_ReturnsHash()
    {
        var hash = ManifestHash.Parse("abc123");
        Assert.Equal("abc123", hash.Value);
    }

    [Fact]
    public void Parse_EmptyString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ManifestHash.Parse(""));
    }

    [Fact]
    public void Parse_WhitespaceString_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ManifestHash.Parse("   "));
    }

    [Fact]
    public void TryParse_ValidValue_ReturnsTrueAndHash()
    {
        var success = ManifestHash.TryParse("testhash", out var result);
        Assert.True(success);
        Assert.Equal("testhash", result.Value);
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        var success = ManifestHash.TryParse("", out var result);
        Assert.False(success);
        Assert.Equal(default, result);
    }

    [Fact]
    public void TryParse_WhitespaceString_ReturnsFalse()
    {
        var success = ManifestHash.TryParse("  ", out var result);
        Assert.False(success);
        Assert.Equal(default, result);
    }

    [Fact]
    public void FromManifest_SameInputs_ReturnsSameHash()
    {
        var manifest = new ChapterManifest
        {
            SeriesId = "series-1",
            ScanGroup = "tcb",
            Language = "en",
            ChapterNumber = 1.0,
            ChapterId = "s:1",
            Title = "Chapter 1",
            Files = new List<ChapterFileEntry>
            {
                new ChapterFileEntry { Path = "page1.jpg", Hash = "abc", Size = 100 }
            }
        };

        var h1 = ManifestHash.FromManifest(manifest);
        var h2 = ManifestHash.FromManifest(manifest);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void FromManifest_DifferentFiles_ReturnsDifferentHash()
    {
        var base1 = new ChapterManifest
        {
            SeriesId = "s", ScanGroup = "g", Language = "en", ChapterNumber = 1,
            ChapterId = "s:1", Title = "t",
            Files = new List<ChapterFileEntry>
            {
                new ChapterFileEntry { Path = "page1.jpg", Hash = "aaa", Size = 100 }
            }
        };

        var base2 = base1 with
        {
            Files = new List<ChapterFileEntry>
            {
                new ChapterFileEntry { Path = "page2.jpg", Hash = "bbb", Size = 200 }
            }
        };

        Assert.NotEqual(ManifestHash.FromManifest(base1), ManifestHash.FromManifest(base2));
    }

    [Fact]
    public void FromManifest_NullManifest_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ManifestHash.FromManifest(null!));
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var h1 = new ManifestHash("abc");
        var h2 = new ManifestHash("abc");
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var h1 = new ManifestHash("abc");
        var h2 = new ManifestHash("xyz");
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void FromManifest_FileOrderIndependent_ReturnsSameHash()
    {
        // Files sorted by path, so order shouldn't matter
        var files1 = new List<ChapterFileEntry>
        {
            new ChapterFileEntry { Path = "page1.jpg", Hash = "aaa", Size = 1 },
            new ChapterFileEntry { Path = "page2.jpg", Hash = "bbb", Size = 2 }
        };
        var files2 = new List<ChapterFileEntry>
        {
            new ChapterFileEntry { Path = "page2.jpg", Hash = "bbb", Size = 2 },
            new ChapterFileEntry { Path = "page1.jpg", Hash = "aaa", Size = 1 }
        };

        var m1 = new ChapterManifest
        {
            SeriesId = "s", ScanGroup = "g", Language = "en", ChapterNumber = 1,
            ChapterId = "s:1", Title = "t", Files = files1
        };
        var m2 = m1 with { Files = files2 };

        Assert.Equal(ManifestHash.FromManifest(m1), ManifestHash.FromManifest(m2));
    }
}
