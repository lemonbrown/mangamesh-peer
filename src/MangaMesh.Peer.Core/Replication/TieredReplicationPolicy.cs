using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Core.Replication;

/// <summary>
/// Age-tiered replication policy.
/// New chapters get high replica counts; archival chapters get the minimum required.
/// Super-seeder nodes always target the new-release count regardless of age.
/// </summary>
public sealed class TieredReplicationPolicy : IReplicationPolicy
{
    private readonly ReplicationOptions _opts;

    public TieredReplicationPolicy(IOptions<ReplicationOptions> options)
    {
        _opts = options.Value;
    }

    public ChunkReplicaTarget GetTarget(ChapterManifest manifest)
    {
        return GetTargetForAge(DateTime.UtcNow - manifest.CreatedUtc);
    }

    public ChunkReplicaTarget GetTargetForAge(TimeSpan age)
    {
        if (_opts.IsSuperSeeder)
            return new ChunkReplicaTarget(_opts.NewReleaseTargetReplicas, _opts.AbsoluteMinimumReplicas);

        if (age.TotalDays <= _opts.NewReleaseAgeDays)
            return new ChunkReplicaTarget(_opts.NewReleaseTargetReplicas, _opts.ArchivalMinimumReplicas);

        if (age.TotalDays <= _opts.ActiveAgeDays)
            return new ChunkReplicaTarget(_opts.ActiveTargetReplicas, _opts.ArchivalMinimumReplicas);

        if (age.TotalDays <= _opts.ActiveAgeDays * 3)
            return new ChunkReplicaTarget(_opts.OlderTargetReplicas, _opts.AbsoluteMinimumReplicas);

        return new ChunkReplicaTarget(_opts.ArchivalMinimumReplicas, _opts.AbsoluteMinimumReplicas);
    }
}
