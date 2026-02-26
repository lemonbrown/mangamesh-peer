using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Node
{
    public interface IDhtMaintenanceService
    {
        void Start();
        void Stop();
    }
}
