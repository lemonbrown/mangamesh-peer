using MangaMesh.Peer.ClientApi.Models;
using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;

namespace MangaMesh.Peer.ClientApi.Services
{
    public class ImportChapterServiceWrapper : IImportChapterService
    {
        private readonly ImportChapterService _importService;
        private readonly SeriesCoverStore _coverStore;
        private readonly IMangaMetadataProvider _metadataProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ImportChapterServiceWrapper> _logger;

        public ImportChapterServiceWrapper(
            ImportChapterService importService,
            SeriesCoverStore coverStore,
            IMangaMetadataProvider metadataProvider,
            IHttpClientFactory httpClientFactory,
            ILogger<ImportChapterServiceWrapper> logger)
        {
            _importService = importService;
            _coverStore = coverStore;
            _metadataProvider = metadataProvider;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<ImportResultDto> ImportAsync(ImportChapterRequestDto request, CancellationToken ct = default)
        {
            var result = await _importService.ImportAsync(new ImportChapterRequest
            {
                SeriesId = request.SeriesId,
                ScanlatorId = request.ScanlatorId,
                Language = request.Language,
                ChapterNumber = request.ChapterNumber,
                SourceDirectory = request.SourcePath,
                ReleaseType = request.ReleaseType switch
                {
                    "VerifiedScanlation" => ReleaseType.VerifiedScanlation,
                    "UnverifiedScanlation" => ReleaseType.UnverifiedScanlation,
                    "RoughTranslation" => ReleaseType.RoughTranslation,
                    "Raw" => ReleaseType.Raw,
                    _ => ReleaseType.Unknown
                },
                DisplayName = request.DisplayName,
                Source = request.Source,
                ExternalMangaId = request.ExternalMangaId,
                Quality = request.Quality
            }, ct);

            // Always try to fetch cover (even if already exists) using the tracker-resolved seriesId
            _ = TryFetchCoverAsync(result.SeriesId, request.Source, request.ExternalMangaId);

            if (result.AlreadyExists)
                throw new InvalidOperationException("Manifest already exists");

            return new ImportResultDto(
                ManifestHash: result.ManifestHash.Value,
                FilesImported: result.FileCount
            );
        }

        public async Task ReannounceAsync(ManifestHash hash, string nodeId)
        {
            await _importService.ReannounceAsync(hash, nodeId);
        }

        private async Task TryFetchCoverAsync(string seriesId, ExternalMetadataSource source, string externalMangaId)
        {
            if (_coverStore.HasCover(seriesId))
            {
                _logger.LogDebug("Cover already exists for series {SeriesId}, skipping fetch", seriesId);
                return;
            }

            if (source != ExternalMetadataSource.MangaDex || string.IsNullOrEmpty(externalMangaId))
            {
                _logger.LogInformation("Skipping cover fetch for series {SeriesId}: source={Source} externalId={ExternalMangaId}", seriesId, source, externalMangaId);
                return;
            }

            try
            {
                _logger.LogInformation("Fetching cover for series {SeriesId} from MangaDex manga {ExternalMangaId}", seriesId, externalMangaId);
                var metadata = await _metadataProvider.GetMangaAsync(externalMangaId);
                if (metadata?.CoverFilename == null)
                {
                    _logger.LogWarning("No cover filename returned for manga {ExternalMangaId}", externalMangaId);
                    return;
                }

                var coverUrl = $"https://uploads.mangadex.org/covers/{externalMangaId}/{metadata.CoverFilename}.256.jpg";
                _logger.LogInformation("Downloading cover from {CoverUrl}", coverUrl);
                var client = _httpClientFactory.CreateClient();
                using var response = await client.GetAsync(coverUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Cover download failed: {StatusCode} for {CoverUrl}", response.StatusCode, coverUrl);
                    return;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                await _coverStore.SaveAsync(seriesId, stream, ".jpg");
                _logger.LogInformation("Saved cover for series {SeriesId}", seriesId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch cover for series {SeriesId}", seriesId);
            }
        }
    }

}
