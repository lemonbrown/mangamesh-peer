using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Transport
{
    public class RoutingEntry
    {
        public byte[] NodeId { get; set; } = Array.Empty<byte>();
        public NodeAddress Address { get; set; } = new("", 0);
        public DateTime LastSeenUtc { get; set; }

        // Storage profile — populated from DhtMessage gossip fields
        public long StorageCapacityBytes { get; set; }
        public long StorageUsedBytes { get; set; }
        public byte BandwidthClass { get; set; }
        public byte UptimeScore { get; set; }
        public bool IsSuperSeeder { get; set; }

        public long FreeStorageBytes => Math.Max(0L, StorageCapacityBytes - StorageUsedBytes);
    }
}
