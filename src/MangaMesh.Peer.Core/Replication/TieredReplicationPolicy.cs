using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Core.Replication;

/// <summary>
/// Age-tiered replication policy with dynamic K based on swarm size.
///
/// K = clamp(swarmSize / RedundancyFactor, 1, MaxTargetReplicas)
///
/// New releases get K * NewReleaseBoost. Older chapters scale down toward K/2.
/// Archival chapters target K=1 (seeder only). MinimumReplicas is always 1.
/// Super-seeder nodes always use the full boosted K regardless of age.
/// </summary>
public sealed class TieredReplicationPolicy : IReplicationPolicy
{
    private readonly ReplicationOptions _opts;
    private readonly IRoutingTable _routingTable;

    public TieredReplicationPolicy(IOptions<ReplicationOptions> options, IRoutingTable routingTable)
    {
        _opts = options.Value;
        _routingTable = routingTable;
    }

    public int GetBaseTargetReplicas()
    {
        // +1 to include self in swarm count
        int swarmSize = _routingTable.GetAll().Count + 1;
        return Math.Clamp(swarmSize / _opts.RedundancyFactor, 1, _opts.MaxTargetReplicas);
    }

    public ChunkReplicaTarget GetTarget(ChapterManifest manifest)
    {
        return GetTargetForAge(DateTime.UtcNow - manifest.CreatedUtc);
    }

    public ChunkReplicaTarget GetTargetForAge(TimeSpan age)
    {
        int k = GetBaseTargetReplicas();

        if (_opts.IsSuperSeeder)
            return new ChunkReplicaTarget(Math.Clamp((int)(k * _opts.NewReleaseBoost), 1, _opts.MaxTargetReplicas), 1);

        if (age.TotalDays <= _opts.NewReleaseAgeDays)
            return new ChunkReplicaTarget(Math.Clamp((int)(k * _opts.NewReleaseBoost), 1, _opts.MaxTargetReplicas), 1);

        if (age.TotalDays <= _opts.ActiveAgeDays)
            return new ChunkReplicaTarget(k, 1);

        if (age.TotalDays <= _opts.ActiveAgeDays * 3)
            return new ChunkReplicaTarget(Math.Max(1, k / 2), 1);

        // Archival: seeder is the sole guaranteed copy
        return new ChunkReplicaTarget(1, 1);
    }
}
