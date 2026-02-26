using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Node
{
    public interface IDhtNode
    {
        INodeIdentity Identity { get; }
        ITransport Transport { get; }
        IRoutingTable RoutingTable { get; }

        void Start(bool enableBootstrap = true, List<RoutingEntry>? bootstrapNodes = null);
        void Stop();

        Task StoreAsync(byte[] contentHash);
        Task<List<byte[]>> FindValueAsync(byte[] contentHash);
        Task<List<DhtNode.ProviderInfo>> FindValueWithAddressAsync(byte[] contentHash);
        Task<List<RoutingEntry>> FindNodeAsync(byte[] nodeId, RoutingEntry? bootstrapNode = null);
        Task PingAsync(RoutingEntry node);

        Task<ContentMessage?> SendContentRequestAsync(NodeAddress address, ContentMessage message, TimeSpan timeout);
        void HandleContentMessage(ContentMessage message);
        Task HandleMessageAsync(DhtMessage message);
    }
}
