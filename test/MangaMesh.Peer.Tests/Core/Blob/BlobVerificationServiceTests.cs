using MangaMesh.Peer.Core.Blob;
using System.Security.Cryptography;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Blob;

public class BlobVerificationServiceTests
{
    [Fact]
    public async Task VerifyBlobAsync_CorrectHash_ReturnsTrue()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("verify me");
        var expectedHashBytes = SHA256.HashData(data);
        var expectedHash = new BlobHash(Convert.ToHexString(expectedHashBytes).ToLowerInvariant());

        using var stream = new MemoryStream(data);
        var result = await BlobVerificationService.VerifyBlobAsync(stream, expectedHash);

        Assert.True(result);
    }

    [Fact]
    public async Task VerifyBlobAsync_WrongHash_ReturnsFalse()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("content");
        var wrongHash = new BlobHash("a".PadRight(64, 'b'));

        using var stream = new MemoryStream(data);
        var result = await BlobVerificationService.VerifyBlobAsync(stream, wrongHash);

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyBlobAsync_EmptyStream_MatchesEmptyHash()
    {
        var data = Array.Empty<byte>();
        var expectedHashBytes = SHA256.HashData(data);
        var expectedHash = new BlobHash(Convert.ToHexString(expectedHashBytes).ToLowerInvariant());

        using var stream = new MemoryStream(data);
        var result = await BlobVerificationService.VerifyBlobAsync(stream, expectedHash);

        Assert.True(result);
    }
}
