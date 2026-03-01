using MangaMesh.Peer.Core.Transport;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    public class DhtProtocolHandler : IProtocolHandler
    {
        public IDhtNode? DhtNode { get; set; }

        private readonly ILogger<DhtProtocolHandler> _logger;

        public DhtProtocolHandler(ILogger<DhtProtocolHandler> logger)
        {
            _logger = logger;
        }

        public ProtocolKind Kind => ProtocolKind.Dht;

        public async Task HandleAsync(NodeAddress from, ReadOnlyMemory<byte> payload)
        {
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(payload.Span);
                var message = JsonSerializer.Deserialize<DhtMessage>(json);
                if (message != null)
                {
                    message.ComputedSenderIp = from.Host;
                    if (DhtNode != null)
                    {
                        await DhtNode.HandleMessageAsync(message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error handling DHT message from {Host}", from.Host);
            }
        }
    }
}
