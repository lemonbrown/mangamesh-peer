using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.Core.Chapters
{
    public class ChapterIngestionService : IChapterIngestionService
    {
        private readonly IEnumerable<IChapterSourceReader> _sourceReaders;
        private readonly IImageFormatProvider _imageFormats;
        private readonly IChunkIngester _chunkIngester;
        private readonly IDhtNode? _dhtNode;

        public ChapterIngestionService(
            IEnumerable<IChapterSourceReader> sourceReaders,
            IImageFormatProvider imageFormats,
            IChunkIngester chunkIngester,
            IDhtNode? dhtNode = null)
        {
            _sourceReaders = sourceReaders;
            _imageFormats = imageFormats;
            _chunkIngester = chunkIngester;
            _dhtNode = dhtNode;
        }

        public async Task<(List<ChapterFileEntry> Entries, long TotalSize)> IngestDirectoryAsync(string sourceDirectory, CancellationToken ct = default)
        {
            var reader = _sourceReaders.FirstOrDefault(r => r.CanRead(sourceDirectory))
                ?? throw new DirectoryNotFoundException($"Source path not found or unsupported: {sourceDirectory}");

            List<ChapterFileEntry> entries = new();
            long totalSize = 0;

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
                        foreach (var chunkHash in pageManifest.Chunks)
                            await _dhtNode.StoreAsync(Convert.FromHexString(chunkHash));
                    }
                }
            }

            return (entries, totalSize);
        }
    }
}
