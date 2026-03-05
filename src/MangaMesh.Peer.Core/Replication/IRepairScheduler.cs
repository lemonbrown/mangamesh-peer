namespace MangaMesh.Peer.Core.Replication;

public interface IRepairScheduler
{
    /// <summary>Scans all locally known chapters and repairs any under-replicated chunks.</summary>
    Task ScanAndRepairAsync(CancellationToken ct = default);

    /// <summary>Repairs a single chapter identified by its manifest hash.</summary>
    Task RepairChapterAsync(string manifestHash, CancellationToken ct = default);
}
