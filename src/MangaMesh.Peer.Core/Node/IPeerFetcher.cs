using MangaMesh.Peer.Core.Transport;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.Core.Node
{
    public interface IPeerFetcher
    {
        /// <summary>
        /// Fetches and stores the chapter manifest locally, registering page blob hashes
        /// in the source provider cache so subsequent blob requests can be proxied on demand.
        /// Returns the resolved hash and the node ID of the peer that delivered the content,
        /// or null for DeliveredByNodeId if the manifest was already cached locally.
        /// </summary>
        Task<(ManifestHash Hash, string? DeliveredByNodeId)> FetchManifestAsync(string manifestHash);

        /// <summary>
        /// Fetches a single blob from a specific peer and returns the raw bytes.
        /// The blob is NOT stored locally — callers are responsible for streaming it
        /// to the client and discarding it.
        /// </summary>
        Task<byte[]?> FetchBlobForProxyAsync(NodeAddress source, string blobHash, CancellationToken ct = default);
    }
}
