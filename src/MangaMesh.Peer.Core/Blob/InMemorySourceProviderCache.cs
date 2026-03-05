using MangaMesh.Peer.Core.Transport;
using System.Collections.Concurrent;

namespace MangaMesh.Peer.Core.Blob;

/// <summary>
/// In-memory mapping of blob hash → provider address.
/// Bounded to prevent unbounded growth; oldest entries are silently dropped when full.
/// </summary>
public sealed class InMemorySourceProviderCache : ISourceProviderCache
{
    // Stores only the provider address — no blob data.
    private readonly ConcurrentDictionary<string, NodeAddress> _map =
        new(StringComparer.OrdinalIgnoreCase);

    private const int MaxEntries = 50_000;

    public void RegisterSource(string blobHash, NodeAddress sourceAddress)
    {
        if (_map.Count >= MaxEntries) return; // drop if full; next manifest fetch will re-register
        _map[blobHash] = sourceAddress;
    }

    public void RegisterSources(IEnumerable<string> blobHashes, NodeAddress sourceAddress)
    {
        foreach (var hash in blobHashes)
            RegisterSource(hash, sourceAddress);
    }

    public NodeAddress? GetSource(string blobHash) =>
        _map.TryGetValue(blobHash, out var addr) ? addr : null;
}
