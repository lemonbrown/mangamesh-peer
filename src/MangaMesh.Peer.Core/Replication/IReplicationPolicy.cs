using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.Core.Replication;

public interface IReplicationPolicy
{
    /// <summary>Returns the replica target for a chapter based on its age.</summary>
    ChunkReplicaTarget GetTarget(ChapterManifest manifest);

    /// <summary>Returns the replica target for a given chapter age, without a full manifest.</summary>
    ChunkReplicaTarget GetTargetForAge(TimeSpan age);
}
