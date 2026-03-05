namespace MangaMesh.Peer.Core.Configuration;

public sealed class EvictionOptions
{
    /// Weight applied to recent access rate (higher access = more popular = keep).
    public double PopularityWeight { get; set; } = 0.4;

    /// Weight applied to rarity (fewer replicas = more rare = keep).
    public double RarityWeight { get; set; } = 0.4;

    /// Weight applied to recency of last access (older = lower score = evict sooner).
    public double AgeWeight { get; set; } = 0.2;

    /// Popularity score reference window in days for normalisation.
    public int PopularityWindowDays { get; set; } = 30;
}
