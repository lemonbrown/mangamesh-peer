using MangaMesh.Peer.Core.Helpers;
using MangaMesh.Peer.Core.Node;
using NSec.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Transport
{
    public class DhtMessage
    {
        public DhtMessageType Type { get; set; }
        public byte[] SenderNodeId { get; set; }
        public byte[] Payload { get; set; }
        public DateTime TimestampUtc { get; set; }
        public byte[] Signature { get; set; }
        
        public Guid RequestId { get; set; } = Guid.NewGuid();

        public int SenderPort { get; set; }

        /// <summary>
        /// The HTTP API port of the sending peer's ClientApi (e.g. 5202).
        /// 0 means not announced (old peers or non-serving nodes).
        /// </summary>
        public int SenderHttpApiPort { get; set; }

        /// <summary>
        /// Advertises that this peer supports WebRTC DataChannel transport.
        /// Receivers should update their routing table entry accordingly.
        /// </summary>
        public bool SupportsWebRtc { get; set; }

        // Storage profile — gossipped with every message so receivers can make
        // replication decisions without a separate round-trip.

        /// <summary>Total configured storage quota in bytes. 0 = not advertised.</summary>
        public long StorageCapacityBytes { get; set; }

        /// <summary>Bytes currently used for blob storage.</summary>
        public long StorageUsedBytes { get; set; }

        /// <summary>Bandwidth tier: 0=Low, 1=Medium, 2=High (maps to BandwidthClass enum).</summary>
        public byte BandwidthClass { get; set; }

        /// <summary>Uptime score 0–100. Higher = more reliable.</summary>
        public byte UptimeScore { get; set; }

        /// <summary>True if this peer voluntarily seeds full series.</summary>
        public bool IsSuperSeeder { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string ComputedSenderIp { get; set; } = string.Empty;

        public bool Verify(INodeIdentity senderIdentity)
        {
            return senderIdentity.Verify(
                Crypto.Hash(Type.ToString(), Payload, TimestampUtc, RequestId.ToByteArray()),
                Signature
            );
        }
    }
}
