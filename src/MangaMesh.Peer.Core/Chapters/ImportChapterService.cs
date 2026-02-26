using MangaMesh.Shared.Models;
using Microsoft.Extensions.Logging;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Peer.Core.Manifests;

namespace MangaMesh.Peer.Core.Chapters
{
    public class ImportChapterService : IImportChapterService
    {
        private readonly IChapterIngestionService _ingestionService;
        private readonly ISeriesRegistry _seriesRegistry;
        private readonly IChapterPublisherService _publisherService;
        private readonly ILogger<ImportChapterService> _logger;

        public ImportChapterService(
            IChapterIngestionService ingestionService,
            ISeriesRegistry seriesRegistry,
            IChapterPublisherService publisherService,
            ILogger<ImportChapterService> logger)
        {
            _ingestionService = ingestionService;
            _seriesRegistry = seriesRegistry;
            _publisherService = publisherService;
            _logger = logger;
        }

        public async Task<ImportChapterResult> ImportAsync(ImportChapterRequest request, CancellationToken ct = default)
        {
            var (entries, totalSize) = await _ingestionService.IngestDirectoryAsync(request.SourceDirectory, ct);

            var (seriesId, seriesTitle) = await _seriesRegistry.RegisterSeriesAsync(request.Source, request.ExternalMangaId);

            var (hash, alreadyExists) = await _publisherService.PublishChapterAsync(
                request, seriesId, seriesTitle, entries, totalSize, ct);

            if (alreadyExists)
            {
                throw new InvalidOperationException("Manifest already exists");
            }

            return new ImportChapterResult
            {
                ManifestHash = hash,
                FileCount = entries.Count,
                AlreadyExists = alreadyExists
            };
        }

        public Task ReannounceAsync(ManifestHash hash, string nodeId)
        {
            return _publisherService.ReannounceAsync(hash, nodeId);
        }
    }
}
