using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Configuration;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Core.Replication;

/// <summary>
/// Ranks blobs for eviction using a weighted score of popularity, rarity, and age.
/// Blobs with replica count below the absolute minimum are never evicted.
/// </summary>
public sealed class ScoredEvictionPolicy : IEvictionPolicy
{
    private readonly IChapterHealthMonitor _healthMonitor;
    private readonly EvictionOptions _evictOpts;
    private readonly ReplicationOptions _replOpts;

    public ScoredEvictionPolicy(
        IChapterHealthMonitor healthMonitor,
        IOptions<EvictionOptions> evictOptions,
        IOptions<ReplicationOptions> replOptions)
    {
        _healthMonitor = healthMonitor;
        _evictOpts = evictOptions.Value;
        _replOpts = replOptions.Value;
    }

    public async IAsyncEnumerable<EvictionCandidate> GetEvictionCandidatesAsync(
        IEnumerable<BlobHash> allBlobs,
        Func<BlobHash, long> getSize,
        Func<BlobHash, DateTime> getLastAccessed,
        long bytesNeeded,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;
        double windowSeconds = TimeSpan.FromDays(_evictOpts.PopularityWindowDays).TotalSeconds;

        var candidates = allBlobs
            .Select(hash =>
            {
                long size = getSize(hash);
                DateTime lastAccess = getLastAccessed(hash);
                int replicas = _healthMonitor.EstimateReplicaCount(hash.Value);
                bool isProtected = replicas > 0 && replicas < _replOpts.AbsoluteMinimumReplicas;

                double ageSeconds = (now - lastAccess).TotalSeconds;

                // Popularity: 1.0 = accessed very recently; 0.0 = accessed long ago
                double popularity = Math.Max(0.0, 1.0 - ageSeconds / windowSeconds);

                // Rarity: 1.0 = very rare (low replicas); 0.0 = well replicated
                double rarity = replicas <= 0
                    ? 0.5  // unknown — treat as moderately rare
                    : Math.Max(0.0, 1.0 - (double)replicas / _replOpts.ActiveTargetReplicas);

                // Age weight: more recently accessed = higher score = keep
                double ageNorm = popularity; // reuse popularity as recency proxy

                double score =
                    (_evictOpts.PopularityWeight * popularity) +
                    (_evictOpts.RarityWeight     * rarity) +
                    (_evictOpts.AgeWeight        * ageNorm);

                return new EvictionCandidate(hash.Value, size, score, replicas, isProtected);
            })
            .Where(c => !c.IsProtected)
            .OrderBy(c => c.Score) // lowest score → evict first
            .ToList();

        long freed = 0;
        foreach (EvictionCandidate candidate in candidates)
        {
            if (freed >= bytesNeeded || ct.IsCancellationRequested)
                yield break;

            yield return candidate;
            freed += candidate.SizeBytes;
        }

        await Task.CompletedTask; // satisfy async enumerable
    }
}
