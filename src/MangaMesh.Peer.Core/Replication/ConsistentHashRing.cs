using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Transport;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace MangaMesh.Peer.Core.Replication;

/// <summary>
/// Deterministic consistent hash ring using SHA-256 positions.
/// Each peer occupies <see cref="ReplicationOptions.ConsistentHashVirtualNodes"/> virtual nodes
/// to achieve an even distribution across the ring.
/// Chunk → N successor peers is computed without any network coordination.
/// </summary>
public sealed class ConsistentHashRing : IConsistentHashRing
{
    private readonly IRoutingTable _routingTable;
    private readonly INodeIdentity _identity;
    private readonly int _virtualNodes;

    // Sorted ring: position (byte[]) → (RoutingEntry, virtualIndex)
    private (byte[] Position, RoutingEntry Entry)[] _ring = [];
    private DateTime _lastRebuild = DateTime.MinValue;
    private readonly object _rebuildLock = new();

    public ConsistentHashRing(
        IRoutingTable routingTable,
        INodeIdentity identity,
        IOptions<ReplicationOptions> options)
    {
        _routingTable = routingTable;
        _identity = identity;
        _virtualNodes = options.Value.ConsistentHashVirtualNodes;
    }

    public IReadOnlyList<RoutingEntry> GetResponsiblePeers(string chunkBlobHash, int replicaCount)
    {
        EnsureRingFresh();

        if (_ring.Length == 0)
            return Array.Empty<RoutingEntry>();

        byte[] chunkPosition = HashPosition(chunkBlobHash);
        int startIndex = FindSuccessorIndex(chunkPosition);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<RoutingEntry>(replicaCount);
        int checked_ = 0;

        for (int i = 0; i < _ring.Length && result.Count < replicaCount; i++)
        {
            int idx = (startIndex + i) % _ring.Length;
            var entry = _ring[idx].Entry;
            string nodeId = Convert.ToHexString(entry.NodeId).ToLowerInvariant();

            if (seen.Add(nodeId))
                result.Add(entry);

            checked_++;
            if (checked_ > _ring.Length)
                break;
        }

        return result;
    }

    public bool IsLocallyResponsible(string chunkBlobHash, int replicaCount)
    {
        string localId = Convert.ToHexString(_identity.NodeId).ToLowerInvariant();
        return GetResponsiblePeers(chunkBlobHash, replicaCount)
            .Any(e => Convert.ToHexString(e.NodeId).ToLowerInvariant() == localId);
    }

    private void EnsureRingFresh()
    {
        if ((DateTime.UtcNow - _lastRebuild).TotalSeconds < 30)
            return;

        lock (_rebuildLock)
        {
            if ((DateTime.UtcNow - _lastRebuild).TotalSeconds < 30)
                return;

            RebuildRing();
        }
    }

    private void RebuildRing()
    {
        var allPeers = _routingTable.GetAll().ToList();

        // Always include the local node so it can be ring-responsible even before bootstrap
        var localEntry = new RoutingEntry
        {
            NodeId = _identity.NodeId,
            Address = new NodeAddress("localhost", 0),
            LastSeenUtc = DateTime.UtcNow
        };

        if (!allPeers.Any(p => p.NodeId.SequenceEqual(_identity.NodeId)))
            allPeers.Add(localEntry);

        var positions = new List<(byte[] Position, RoutingEntry Entry)>(allPeers.Count * _virtualNodes);

        foreach (RoutingEntry peer in allPeers)
        {
            string nodeHex = Convert.ToHexString(peer.NodeId).ToLowerInvariant();
            for (int v = 0; v < _virtualNodes; v++)
            {
                byte[] pos = HashPosition($"{nodeHex}:{v}");
                positions.Add((pos, peer));
            }
        }

        positions.Sort((a, b) => CompareBytes(a.Position, b.Position));
        _ring = positions.ToArray();
        _lastRebuild = DateTime.UtcNow;
    }

    private int FindSuccessorIndex(byte[] target)
    {
        int lo = 0, hi = _ring.Length - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            int cmp = CompareBytes(_ring[mid].Position, target);
            if (cmp == 0) return mid;
            if (cmp < 0) lo = mid + 1;
            else hi = mid - 1;
        }
        // lo is now the insertion point; wrap around
        return lo % _ring.Length;
    }

    private static byte[] HashPosition(string input)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(input));
    }

    private static int CompareBytes(byte[] a, byte[] b)
    {
        int len = Math.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            int diff = a[i] - b[i];
            if (diff != 0) return diff;
        }
        return a.Length - b.Length;
    }
}
