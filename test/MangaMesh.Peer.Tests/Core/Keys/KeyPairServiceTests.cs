using MangaMesh.Peer.Core.Keys;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NSec.Cryptography;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Keys;

public class KeyPairServiceTests
{
    private readonly Mock<IKeyStore> _keyStore;
    private readonly KeyPairService _sut;

    public KeyPairServiceTests()
    {
        _keyStore = new Mock<IKeyStore>();
        _keyStore
            .Setup(k => k.SaveAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _sut = new KeyPairService(_keyStore.Object, NullLogger<KeyPairService>.Instance);
    }

    [Fact]
    public void GenerateKeyPairBase64_ReturnsValidBase64Keys()
    {
        var result = _sut.GenerateKeyPairBase64();

        Assert.NotNull(result.PublicKeyBase64);
        Assert.NotNull(result.PrivateKeyBase64);

        // Should be valid base64
        var pubBytes = Convert.FromBase64String(result.PublicKeyBase64);
        var privBytes = Convert.FromBase64String(result.PrivateKeyBase64);

        Assert.Equal(32, pubBytes.Length);  // Ed25519 public key
        Assert.Equal(32, privBytes.Length); // Ed25519 private key (seed)
    }

    [Fact]
    public void GenerateKeyPairBase64_TwoCalls_ProduceDifferentKeys()
    {
        var r1 = _sut.GenerateKeyPairBase64();
        var r2 = _sut.GenerateKeyPairBase64();

        Assert.NotEqual(r1.PublicKeyBase64, r2.PublicKeyBase64);
        Assert.NotEqual(r1.PrivateKeyBase64, r2.PrivateKeyBase64);
    }

    [Fact]
    public async Task GenerateKeyPairBase64Async_SavesKeyToStore()
    {
        await _sut.GenerateKeyPairBase64Async();

        _keyStore.Verify(k => k.SaveAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GenerateKeyPairBase64Async_ReturnsValidResult()
    {
        var result = await _sut.GenerateKeyPairBase64Async();

        Assert.NotNull(result.PublicKeyBase64);
        Assert.NotNull(result.PrivateKeyBase64);
    }

    [Fact]
    public void SolveChallenge_ValidKey_ReturnsBase64Signature()
    {
        var (privKey, _, nonce) = GenerateKeyAndNonce();

        var signature = _sut.SolveChallenge(nonce, privKey);

        Assert.False(string.IsNullOrEmpty(signature));
        var sigBytes = Convert.FromBase64String(signature);
        Assert.Equal(64, sigBytes.Length);
    }

    [Fact]
    public void SolveChallenge_InvalidBase64PrivateKey_Throws()
    {
        var nonce = Convert.ToBase64String(new byte[32]);
        Assert.Throws<FormatException>(() => _sut.SolveChallenge(nonce, "not-valid-base64!!!"));
    }

    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        var (privKey, pubKey, nonce) = GenerateKeyAndNonce();

        var signature = _sut.SolveChallenge(nonce, privKey);
        var isValid = _sut.Verify(pubKey, signature, nonce);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_WrongNonce_ReturnsFalse()
    {
        var (privKey, pubKey, nonce) = GenerateKeyAndNonce();

        var signature = _sut.SolveChallenge(nonce, privKey);
        var wrongNonce = Convert.ToBase64String(new byte[32]); // all zeros

        var isValid = _sut.Verify(pubKey, signature, wrongNonce);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_InvalidBase64PublicKey_ReturnsFalse()
    {
        var result = _sut.Verify("not-valid!!!", "dummysig", "dummynonce");
        Assert.False(result);
    }

    [Fact]
    public void Verify_InvalidBase64Signature_ReturnsFalse()
    {
        var (_, pubKey, nonce) = GenerateKeyAndNonce();
        var result = _sut.Verify(pubKey, "not-valid!!!", nonce);
        Assert.False(result);
    }

    [Fact]
    public void Verify_InvalidBase64Nonce_ReturnsFalse()
    {
        var (privKey, pubKey, nonce) = GenerateKeyAndNonce();
        var sig = _sut.SolveChallenge(nonce, privKey);
        var result = _sut.Verify(pubKey, sig, "not-valid!!!");
        Assert.False(result);
    }

    private static (string PrivKeyBase64, string PubKeyBase64, string NonceBase64) GenerateKeyAndNonce()
    {
        var creationParameters = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport
        };

        using var key = new Key(SignatureAlgorithm.Ed25519, creationParameters);
        var privKey = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPrivateKey));
        var pubKey = Convert.ToBase64String(key.Export(KeyBlobFormat.RawPublicKey));

        var nonceBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(nonceBytes);
        var nonce = Convert.ToBase64String(nonceBytes);

        return (privKey, pubKey, nonce);
    }
}
