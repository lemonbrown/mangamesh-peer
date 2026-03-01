using System.Numerics;
using MangaMesh.Peer.Core.Helpers;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Helpers;

public class CryptoTests
{
    [Fact]
    public void Sha256_ReturnsExpectedHash()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("hello");
        var hash = Crypto.Sha256(input);
        Assert.Equal(32, hash.Length);
        // SHA256("hello") = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824
        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
            Convert.ToHexString(hash).ToLowerInvariant());
    }

    [Fact]
    public void Sha256_EmptyInput_Returns32Bytes()
    {
        var hash = Crypto.Sha256(Array.Empty<byte>());
        Assert.Equal(32, hash.Length);
    }

    [Fact]
    public void Sha256_SameInputProducesSameHash()
    {
        var input = System.Text.Encoding.UTF8.GetBytes("deterministic");
        var h1 = Crypto.Sha256(input);
        var h2 = Crypto.Sha256(input);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void XorDistance_SameArrays_ReturnsZero()
    {
        var a = new byte[] { 0xFF, 0xAB, 0x12 };
        var dist = Crypto.XorDistance(a, a);
        Assert.Equal(BigInteger.Zero, dist);
    }

    [Fact]
    public void XorDistance_DifferentArrays_ReturnsNonZero()
    {
        var a = new byte[] { 0x01, 0x00, 0x00, 0x00 };
        var b = new byte[] { 0x00, 0x00, 0x00, 0x01 };
        var dist = Crypto.XorDistance(a, b);
        Assert.True(dist > BigInteger.Zero);
    }

    [Fact]
    public void XorDistance_LengthMismatch_Throws()
    {
        var a = new byte[] { 0x01 };
        var b = new byte[] { 0x01, 0x02 };
        Assert.Throws<ArgumentException>(() => Crypto.XorDistance(a, b));
    }

    [Fact]
    public void XorDistance_IsCommutative()
    {
        var a = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var b = new byte[] { 0x12, 0x34, 0x56, 0x78 };
        Assert.Equal(Crypto.XorDistance(a, b), Crypto.XorDistance(b, a));
    }

    [Fact]
    public void RandomNodeId_Returns32Bytes()
    {
        var id = Crypto.RandomNodeId();
        Assert.Equal(32, id.Length);
    }

    [Fact]
    public void RandomNodeId_TwoCallsProduceDifferentIds()
    {
        var id1 = Crypto.RandomNodeId();
        var id2 = Crypto.RandomNodeId();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Hash_StringItems_Returns32Bytes()
    {
        var hash = Crypto.Hash("series-1", "chapter-1");
        Assert.Equal(32, hash.Length);
    }

    [Fact]
    public void Hash_ByteArrayItems_Returns32Bytes()
    {
        var hash = Crypto.Hash(new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 });
        Assert.Equal(32, hash.Length);
    }

    [Fact]
    public void Hash_SameInputsDeterministic()
    {
        var h1 = Crypto.Hash("a", "b", "c");
        var h2 = Crypto.Hash("a", "b", "c");
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Ed25519Sign_InvalidKeyLength_Throws()
    {
        var badKey = new byte[16]; // Not 32 bytes
        var data = new byte[] { 1, 2, 3 };
        Assert.Throws<ArgumentException>(() => Crypto.Ed25519Sign(badKey, data));
    }

    [Fact]
    public void Ed25519Sign_ValidKey_Returns64Bytes()
    {
        // Generate valid 32-byte Ed25519 seed via KeyPairService logic
        using var key = NSec.Cryptography.Key.Create(
            NSec.Cryptography.SignatureAlgorithm.Ed25519,
            new NSec.Cryptography.KeyCreationParameters
            {
                ExportPolicy = NSec.Cryptography.KeyExportPolicies.AllowPlaintextExport
            });
        var privateKey = key.Export(NSec.Cryptography.KeyBlobFormat.RawPrivateKey);
        var data = new byte[] { 1, 2, 3 };
        var sig = Crypto.Ed25519Sign(privateKey, data);
        Assert.Equal(64, sig.Length);
    }
}
