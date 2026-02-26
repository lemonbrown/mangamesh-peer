namespace MangaMesh.Peer.Core.Transport
{
    /// <summary>
    /// Selects the appropriate transport for communicating with a given peer.
    /// Enables capability-based fallback from WebRTC to TCP.
    /// </summary>
    public interface ITransportSelector
    {
        ITransport SelectFor(NodeAddress target);
    }
}
