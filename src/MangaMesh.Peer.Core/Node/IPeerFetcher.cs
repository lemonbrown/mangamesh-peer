using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    public interface IPeerFetcher
    {
        /// <summary>
        /// Fetches a manifest (and its page blobs) ensuring it is available locally.
        /// Returns the resolved hash and the node ID of the peer that delivered the content,
        /// or null for DeliveredByNodeId if the manifest was already cached locally.
        /// </summary>
        Task<(ManifestHash Hash, string? DeliveredByNodeId)> FetchManifestAsync(string manifestHash);
    }
}
