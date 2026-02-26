using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Node
{
    public interface IDhtLookupService
    {
        Task<List<byte[]>> FindValueAsync(byte[] contentHash);
        Task<List<DhtNode.ProviderInfo>> FindValueWithAddressAsync(byte[] contentHash);
        Task<List<RoutingEntry>> FindNodeAsync(byte[] nodeId, RoutingEntry? bootstrap = null);
    }
}
