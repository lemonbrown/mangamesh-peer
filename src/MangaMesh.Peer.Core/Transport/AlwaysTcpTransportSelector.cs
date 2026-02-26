namespace MangaMesh.Peer.Core.Transport
{
    /// <summary>
    /// ITransportSelector that always returns TCP. Used when WebRTC is disabled
    /// so DhtNode has zero behavior change without conditional logic at call sites.
    /// </summary>
    public class AlwaysTcpTransportSelector : ITransportSelector
    {
        private readonly ITransport _transport;

        public AlwaysTcpTransportSelector(ITransport transport)
        {
            _transport = transport;
        }

        public ITransport SelectFor(NodeAddress target) => _transport;
    }
}
