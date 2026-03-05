using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Replication;

/// <summary>
/// Scores peers for replication target selection using a weighted composite score.
/// Higher score = better replication target.
///
/// Score = (freeStorageRatio × 0.35)
///       + (normalizedUptime  × 0.25)
///       + (bandwidthFactor   × 0.20)
///       + (geoDiversityBonus × 0.10)
///       - (regionPenalty     × 0.10)
/// </summary>
public sealed class WeightedPeerScorer : IPeerScorer
{
    public IReadOnlyList<RoutingEntry> RankCandidates(IEnumerable<RoutingEntry> candidates, int count)
    {
        return candidates
            .Select(e => (Entry: e, Score: ComputeScore(e)))
            .OrderByDescending(x => x.Score)
            .Take(count)
            .Select(x => x.Entry)
            .ToList();
    }

    private static double ComputeScore(RoutingEntry entry)
    {
        double freeStorageRatio = ComputeFreeStorageRatio(entry);
        double normalizedUptime = entry.UptimeScore / 100.0;
        double bandwidthFactor = ComputeBandwidthFactor(entry);

        // Geo/region diversity is a stub — always 0 until ISP/ASN data is available
        const double geoDiversityBonus = 0.0;
        const double regionPenalty = 0.0;

        return (freeStorageRatio * 0.35)
             + (normalizedUptime * 0.25)
             + (bandwidthFactor * 0.20)
             + (geoDiversityBonus * 0.10)
             - (regionPenalty * 0.10);
    }

    private static double ComputeFreeStorageRatio(RoutingEntry entry)
    {
        if (entry.StorageCapacityBytes <= 0)
            return 0.5; // unknown capacity — assume middle ground

        long free = Math.Max(0L, entry.StorageCapacityBytes - entry.StorageUsedBytes);
        return Math.Min(1.0, (double)free / entry.StorageCapacityBytes);
    }

    private static double ComputeBandwidthFactor(RoutingEntry entry) =>
        entry.BandwidthClass switch
        {
            (byte)BandwidthClass.High   => 1.0,
            (byte)BandwidthClass.Medium => 0.6,
            _                           => 0.2
        };
}
