using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;

namespace MangaMesh.Peer.Core.Replication;

/// <summary>
/// A simple Bloom filter for tracking chunk ownership in Gossip messages.
/// Helps nodes discover which remote peers own which chunks without massive overhead.
/// </summary>
public sealed class BloomFilter
{
    private readonly BitArray _bits;
    private readonly int _hashFunctions;

    public byte[] ToByteArray()
    {
        int bytesCount = (_bits.Length + 7) / 8;
        byte[] bytes = new byte[bytesCount];
        _bits.CopyTo(bytes, 0);
        return bytes;
    }

    public BloomFilter(int capacity, double errorRate = 0.05)
    {
        int bitSize = Math.Max(1, (int)Math.Ceiling(capacity * Math.Log(errorRate) / Math.Log(1.0 / Math.Pow(2.0, Math.Log(2.0)))));
        _bits = new BitArray(bitSize);
        _hashFunctions = Math.Max(1, (int)Math.Round(Math.Log(2.0) * bitSize / capacity));
    }

    public BloomFilter(byte[] data, int hashFunctions)
    {
        _bits = new BitArray(data);
        _hashFunctions = hashFunctions;
    }

    public void Add(string item)
    {
        foreach (int hash in GetHashes(item))
        {
            _bits[hash % _bits.Length] = true;
        }
    }

    public bool Contains(string item)
    {
        foreach (int hash in GetHashes(item))
        {
            if (!_bits[hash % _bits.Length])
                return false;
        }
        return true;
    }

    private IEnumerable<int> GetHashes(string item)
    {
        byte[] itemBytes = Encoding.UTF8.GetBytes(item);
        using var md5 = MD5.Create();
        byte[] hashResult = md5.ComputeHash(itemBytes);

        // Extract multiple 32-bit integers from the MD5 hash
        int h1 = BitConverter.ToInt32(hashResult, 0);
        int h2 = BitConverter.ToInt32(hashResult, 4);

        for (int i = 0; i < _hashFunctions; i++)
        {
            int combinedHash = h1 + (i * h2);
            yield return Math.Abs(combinedHash);
        }
    }
}
