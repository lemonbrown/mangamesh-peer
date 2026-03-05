using MangaMesh.Peer.Core.Blob;

namespace MangaMesh.Peer.Core.Replication;

public interface IEvictionPolicy
{
    /// <summary>
    /// Returns eviction candidates in priority order (lowest score first) until
    /// <paramref name="bytesNeeded"/> can be freed.
    /// Protected blobs (replica count below minimum) are never returned.
    /// </summary>
    IAsyncEnumerable<EvictionCandidate> GetEvictionCandidatesAsync(
        IEnumerable<BlobHash> allBlobs,
        Func<BlobHash, long> getSize,
        Func<BlobHash, DateTime> getLastAccessed,
        long bytesNeeded,
        CancellationToken ct = default);
}
