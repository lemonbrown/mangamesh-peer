using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.Core.Chapters
{
    public interface IChapterIngestionService
    {
        Task<(List<ChapterFileEntry> Entries, long TotalSize)> IngestDirectoryAsync(string sourceDirectory, CancellationToken ct = default);
    }
}
