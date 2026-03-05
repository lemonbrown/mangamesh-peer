using MangaMesh.Peer.Core.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace MangaMesh.Peer.Core.Replication;

/// <summary>
/// Enforces the diversity constraint: the local peer may not hold more than
/// <see cref="ReplicationOptions.MaxChunksPerPeerPerChapterRatio"/> of any chapter's chunks.
/// Original importers are exempt — the diversity constraint only applies when
/// accepting replication pushes from other peers.
/// </summary>
public sealed class InMemoryChapterDiversityTracker : IChapterDiversityTracker
{
    private readonly ConcurrentDictionary<string, int> _counts =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly double _maxRatio;

    public InMemoryChapterDiversityTracker(IOptions<ReplicationOptions> options)
    {
        _maxRatio = options.Value.MaxChunksPerPeerPerChapterRatio;
    }

    public bool CanAcceptChunk(string chapterId, int totalChunksInChapter)
    {
        if (totalChunksInChapter <= 0)
            return true; // unknown total — allow

        int current = _counts.GetOrAdd(chapterId, 0);
        double ratio = (double)(current + 1) / totalChunksInChapter;
        return ratio <= _maxRatio;
    }

    public void RecordChunkAccepted(string chapterId)
    {
        _counts.AddOrUpdate(chapterId, 1, (_, existing) => existing + 1);
    }

    public int GetLocalChunkCount(string chapterId)
    {
        return _counts.GetOrAdd(chapterId, 0);
    }
}
