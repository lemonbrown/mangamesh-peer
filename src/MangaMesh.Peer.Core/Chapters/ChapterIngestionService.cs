using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Replication;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MangaMesh.Peer.Core.Chapters
{
    public class ChapterIngestionService : IChapterIngestionService
    {
        private readonly IEnumerable<IChapterSourceReader> _sourceReaders;
        private readonly IImageFormatProvider _imageFormats;
        private readonly IChunkIngester _chunkIngester;
        private readonly IDhtNode? _dhtNode;
        private readonly IReplicationExecutor? _replicationExecutor;
        private readonly IReplicationDecisionEngine? _replicationDecision;
        private readonly IChapterHealthMonitor? _healthMonitor;
        private readonly ILogger<ChapterIngestionService>? _logger;
        private readonly IReplicationPolicy? _replicationPolicy;

        public ChapterIngestionService(
            IEnumerable<IChapterSourceReader> sourceReaders,
            IImageFormatProvider imageFormats,
            IChunkIngester chunkIngester,
            IDhtNode? dhtNode = null,
            IReplicationExecutor? replicationExecutor = null,
            IReplicationDecisionEngine? replicationDecision = null,
            IChapterHealthMonitor? healthMonitor = null,
            ILogger<ChapterIngestionService>? logger = null,
            IReplicationPolicy? replicationPolicy = null)
        {
            _sourceReaders = sourceReaders;
            _imageFormats = imageFormats;
            _chunkIngester = chunkIngester;
            _dhtNode = dhtNode;
            _replicationExecutor = replicationExecutor;
            _replicationDecision = replicationDecision;
            _healthMonitor = healthMonitor;
            _logger = logger;
            _replicationPolicy = replicationPolicy;
        }

        public async Task<(List<ChapterFileEntry> Entries, long TotalSize)> IngestDirectoryAsync(
            string sourceDirectory,
            CancellationToken ct = default)
        {
            var reader = _sourceReaders.FirstOrDefault(r => r.CanRead(sourceDirectory))
                ?? throw new DirectoryNotFoundException($"Source path not found or unsupported: {sourceDirectory}");

            List<ChapterFileEntry> entries = new();
            long totalSize = 0;

            // Collect all chunk hashes during ingestion for post-import seeding
            // (ChunkHash, PageHash, TotalChunksInPage)
            var allChunks = new List<(string ChunkHash, string PageHash, int TotalChunks)>();

            await foreach (var (name, content) in reader.ReadFilesAsync(sourceDirectory, ct))
            {
                using (content)
                {
                    var mimeType = _imageFormats.GetMimeType(name);
                    var (pageManifest, pageHash) = await _chunkIngester.IngestAsync(content, mimeType);

                    totalSize += pageManifest.FileSize;

                    entries.Add(new ChapterFileEntry
                    {
                        Hash = pageHash,
                        Path = name,
                        Size = pageManifest.FileSize
                    });

                    // Announce page manifest and all chunks to DHT so the gateway can find them
                    if (_dhtNode != null)
                    {
                        await _dhtNode.StoreAsync(Convert.FromHexString(pageHash));
                        int pageChunkCount = pageManifest.Chunks.Count;
                        foreach (var chunkHash in pageManifest.Chunks)
                        {
                            await _dhtNode.StoreAsync(Convert.FromHexString(chunkHash));
                            allChunks.Add((chunkHash, pageHash, pageChunkCount));
                        }
                    }

                    // Record local ownership in health monitor
                    if (_healthMonitor != null && _dhtNode != null)
                    {
                        string localPeerId = Convert.ToHexString(_dhtNode.Identity.NodeId).ToLowerInvariant();
                        _healthMonitor.RecordChunkOwner(pageHash, localPeerId);
                        foreach (var chunkHash in pageManifest.Chunks)
                            _healthMonitor.RecordChunkOwner(chunkHash, localPeerId);
                    }
                }
            }

            // Fire-and-forget: push chunks to ring peers in background.
            // Skip the ring-leader check (ShouldReplicateChunkAsync) — the seeder is the sole
            // source and must push all chunks immediately regardless of ring position.
            // Also push page manifests so receiving nodes can decode chunk membership in GetOverview.
            if (_replicationExecutor != null && allChunks.Count > 0)
            {
                int targetReplicas = _replicationPolicy?.GetBaseTargetReplicas() ?? 3;
                string chapterKey = allChunks.Select(c => c.PageHash).Distinct().OrderBy(h => h).First();
                int totalChunksInChapter = allChunks.Count;
                var pageHashes = allChunks.Select(c => c.PageHash).Distinct().ToList();

                _ = Task.Run(async () =>
                {
                    // Push page manifests first so receivers can decode image chunk membership
                    foreach (var pageHash in pageHashes)
                    {
                        try { await _replicationExecutor.PushToRingPeersAsync(pageHash, chapterKey, targetReplicas, totalChunksInChapter, ct); }
                        catch (Exception ex) { _logger?.LogDebug(ex, "Post-import page manifest push failed for {Hash}", pageHash[..8]); }
                    }

                    // Push all image chunks
                    foreach (var (chunkHash, _, _) in allChunks)
                    {
                        try { await _replicationExecutor.PushToRingPeersAsync(chunkHash, chapterKey, targetReplicas, totalChunksInChapter, ct); }
                        catch (Exception ex) { _logger?.LogDebug(ex, "Post-import replication push failed for chunk {Hash}", chunkHash[..8]); }
                    }
                }, ct);
            }

            return (entries, totalSize);
        }
    }
}
