using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Node
{
    public interface IDhtMessageHandler
    {
        Task HandleAsync(DhtMessage message);
    }
}
