using MangaMesh.Peer.ClientApi.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace MangaMesh.Peer.Tests.ClientApi.Services;

public class ChallengeServiceTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ChallengeService _sut;

    public ChallengeServiceTests()
    {
        _cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        _sut = new ChallengeService(_cache);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Fact]
    public void CreateChallenge_ReturnsNonEmptyIdAndNonce()
    {
        var (id, nonce) = _sut.CreateChallenge("pubkey123");

        Assert.NotEmpty(id);
        Assert.NotEmpty(nonce);
    }

    [Fact]
    public void CreateChallenge_NonceShouldBeBase64()
    {
        var (_, nonce) = _sut.CreateChallenge("pubkey");

        var bytes = Convert.FromBase64String(nonce);
        Assert.Equal(32, bytes.Length); // 32 random bytes
    }

    [Fact]
    public void CreateChallenge_TwoCalls_ProduceDifferentIdAndNonce()
    {
        var (id1, nonce1) = _sut.CreateChallenge("pubkey");
        var (id2, nonce2) = _sut.CreateChallenge("pubkey");

        Assert.NotEqual(id1, id2);
        Assert.NotEqual(nonce1, nonce2);
    }

    [Fact]
    public void GetNonce_ExistingChallenge_ReturnsNonce()
    {
        var (id, expectedNonce) = _sut.CreateChallenge("pubkey");

        var nonce = _sut.GetNonce(id);

        Assert.Equal(expectedNonce, nonce);
    }

    [Fact]
    public void GetNonce_UnknownChallengeId_ReturnsNull()
    {
        var nonce = _sut.GetNonce("unknown-id");

        Assert.Null(nonce);
    }

    [Fact]
    public void Remove_AfterCreate_NonceIsGone()
    {
        var (id, _) = _sut.CreateChallenge("pubkey");

        _sut.Remove(id);

        var nonce = _sut.GetNonce(id);
        Assert.Null(nonce);
    }

    [Fact]
    public void Remove_NonExistentId_DoesNotThrow()
    {
        var ex = Record.Exception(() => _sut.Remove("nonexistent-id"));
        Assert.Null(ex);
    }

    [Fact]
    public void CreateChallenge_IdIsGuid()
    {
        var (id, _) = _sut.CreateChallenge("pubkey");

        Assert.True(Guid.TryParse(id, out _), $"Expected a GUID but got: {id}");
    }
}
