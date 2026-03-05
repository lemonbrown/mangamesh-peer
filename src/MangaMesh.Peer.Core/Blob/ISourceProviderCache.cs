using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Blob;

/// <summary>
/// Maps blob hashes to the peer address that supplied them during a manifest fetch.
/// This lets the blob controller proxy-fetch content on demand without storing it.
/// Only addresses are cached — no blob data is held.
/// </summary>
public interface ISourceProviderCache
{
    void RegisterSource(string blobHash, NodeAddress sourceAddress);
    void RegisterSources(IEnumerable<string> blobHashes, NodeAddress sourceAddress);
    NodeAddress? GetSource(string blobHash);
}
