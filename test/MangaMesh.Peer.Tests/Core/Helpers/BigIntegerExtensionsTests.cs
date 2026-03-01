using System.Numerics;
using MangaMesh.Peer.Core.Helpers;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Helpers;

public class BigIntegerExtensionsTests
{
    [Fact]
    public void BitLength_Zero_ReturnsZero()
    {
        Assert.Equal(0, BigInteger.Zero.BitLength());
    }

    [Fact]
    public void BitLength_One_ReturnsOne()
    {
        Assert.Equal(1, new BigInteger(1).BitLength());
    }

    [Fact]
    public void BitLength_Two_ReturnsTwo()
    {
        Assert.Equal(2, new BigInteger(2).BitLength());
    }

    [Fact]
    public void BitLength_Three_ReturnsTwo()
    {
        Assert.Equal(2, new BigInteger(3).BitLength());
    }

    [Fact]
    public void BitLength_Four_ReturnsThree()
    {
        Assert.Equal(3, new BigInteger(4).BitLength());
    }

    [Fact]
    public void BitLength_255_ReturnsEight()
    {
        Assert.Equal(8, new BigInteger(255).BitLength());
    }

    [Fact]
    public void BitLength_256_ReturnsNine()
    {
        Assert.Equal(9, new BigInteger(256).BitLength());
    }

    [Fact]
    public void BitLength_LargeValue_ReturnsCorrectBits()
    {
        // 2^32 has 33 bits
        var val = BigInteger.Pow(2, 32);
        Assert.Equal(33, val.BitLength());
    }
}
