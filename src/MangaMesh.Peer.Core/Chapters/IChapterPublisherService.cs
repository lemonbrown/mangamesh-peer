using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.Core.Chapters
{
    public interface IChapterPublisherService
    {
        Task<(ManifestHash Hash, bool AlreadyExists)> PublishChapterAsync(
            ImportChapterRequest request,
            string seriesId,
            string seriesTitle,
            List<ChapterFileEntry> entries,
            long totalSize,
            CancellationToken ct = default);

        Task ReannounceAsync(ManifestHash hash, string nodeId);
    }
}
