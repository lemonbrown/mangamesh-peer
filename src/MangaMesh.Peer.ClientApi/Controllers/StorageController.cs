using MangaMesh.Peer.ClientApi.Models;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Storage;
using MangaMesh.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [Route("api/node/storage")]
    [ApiController]
    [Authorize]
    public class StorageController : ControllerBase
    {
        private readonly IStorageMonitorService _storageMonitorService;
        private readonly IBlobStore _blobStore;

        public StorageController(IStorageMonitorService storageMonitorService, IBlobStore blobStore)
        {
            _storageMonitorService = storageMonitorService;
            _blobStore = blobStore;
        }

        [HttpGet]
        public async Task<ActionResult<StorageStats>> GetStorageStats()
        {
            var stats = await _storageMonitorService.GetStorageStatsAsync();
            stats.BlobCount = _blobStore.GetAllHashes().Count();
            return Ok(stats);
        }

        [HttpGet("manifests")]
        public async Task<ActionResult> GetManifests(
            [FromServices] IManifestStore manifestStore,
            [FromQuery] string? q = null,
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 20)
        {
            var all = await manifestStore.GetAllWithDataAsync();

            IEnumerable<(ManifestHash Hash, ChapterManifest Manifest, bool IsDownloaded)> filtered = all;
            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = all.Where(x =>
                    (x.Manifest.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (x.Manifest.SeriesId?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (x.Manifest.Language?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (x.Manifest.ScanGroup?.Contains(q, StringComparison.OrdinalIgnoreCase) == true));
            }

            var filteredList = filtered.ToList();
            var total = filteredList.Count;
            var items = filteredList
                .Skip(offset)
                .Take(limit)
                .Select(x => new StoredManifestDto(
                    x.Hash.Value,
                    x.Manifest.SeriesId,
                    x.Manifest.ChapterNumber.ToString(),
                    x.Manifest.Volume,
                    x.Manifest.Language,
                    x.Manifest.ScanGroup,
                    x.Manifest.Title,
                    x.Manifest.TotalSize,
                    x.Manifest.Files?.Count ?? 0,
                    x.Manifest.CreatedUtc))
                .ToList();

            return Ok(new { items, total, offset, limit });
        }

        [HttpPost("manifests/{hash}/download")]
        public async Task<ActionResult> DownloadManifest(
            string hash,
            [FromServices] IManifestStore manifestStore,
            [FromServices] IPeerFetcher peerFetcher)
        {
            if (!ManifestHash.TryParse(hash, out var manifestHash))
                return BadRequest("Invalid hash format");

            try
            {
                // Fetch the manifest and its pages from the DHT/Peers
                var (storedHash, _) = await peerFetcher.FetchManifestAsync(hash);

                // Mark it as explicitly downloaded by the user
                await manifestStore.MarkAsDownloadedAsync(storedHash);

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to download manifest: {ex.Message}");
            }
        }

        [HttpDelete("manifests/{hash}")]
        public async Task<ActionResult> DeleteManifest(string hash, [FromServices] IManifestStore manifestStore)
        {
            if (!ManifestHash.TryParse(hash, out var manifestHash))
                return BadRequest("Invalid hash format");

            await manifestStore.DeleteAsync(manifestHash);
            return Ok();
        }

        [HttpDelete("manifests")]
        public async Task<ActionResult> DeleteManifests([FromBody] string[] hashes, [FromServices] IManifestStore manifestStore)
        {
            foreach (var hash in hashes)
            {
                if (ManifestHash.TryParse(hash, out var manifestHash))
                    await manifestStore.DeleteAsync(manifestHash);
            }
            return Ok();
        }

        [HttpDelete("blobs/{hash}")]
        public ActionResult DeleteBlob(string hash)
        {
            _blobStore.Delete(new BlobHash(hash));
            return Ok();
        }

        [HttpDelete("blobs")]
        public ActionResult DeleteBlobs([FromBody] string[] hashes)
        {
            foreach (var hash in hashes)
                _blobStore.Delete(new BlobHash(hash));
            return Ok();
        }

        /// <summary>
        /// Returns a summary of all locally-stored series (title, cover, chapter count).
        /// Used by the Peer UI local library page.
        /// </summary>
        [HttpGet("series")]
        public async Task<ActionResult> GetAllLocalSeries([FromServices] IManifestStore manifestStore)
        {
            var all = await manifestStore.GetAllWithDataAsync();
            var seriesList = all
                .Where(x => x.IsDownloaded)
                .GroupBy(x => x.Manifest.SeriesId)
                .Select(g =>
                {
                    var seriesTitle = g.Select(x => x.Manifest.SeriesTitle).FirstOrDefault(t => !string.IsNullOrEmpty(t));
                    var externalMangaId = g.Select(x => x.Manifest.ExternalMangaId).FirstOrDefault(id => !string.IsNullOrEmpty(id));
                    var chapterCount = g.Select(x => x.Manifest.ChapterId).Distinct().Count();
                    var chapterNumbers = g.Select(x => x.Manifest.ChapterNumber).Where(n => n > 0);
                    var latestChapter = chapterNumbers.Any() ? (double?)chapterNumbers.Max() : null;
                    var totalSizeBytes = g.Sum(x => x.Manifest.TotalSize);
                    return new
                    {
                        seriesId = g.Key,
                        seriesTitle,
                        externalMangaId,
                        chapterCount,
                        latestChapter,
                        totalSizeBytes
                    };
                })
                .OrderBy(s => s.seriesTitle ?? s.seriesId)
                .ToList();
            return Ok(seriesList);
        }

        /// <summary>
        /// Returns all locally-stored chapters and their manifests for a series.
        /// Used by the Peer UI local library — no index/network calls required.
        /// </summary>
        [HttpGet("series/{seriesId}")]
        public async Task<ActionResult> GetLocalSeries(string seriesId, [FromServices] IManifestStore manifestStore, [FromQuery] bool includeReplicated = false)
        {
            var all = await manifestStore.GetAllWithDataAsync();
            var forSeriesQuery = all.Where(x => x.Manifest.SeriesId == seriesId);

            if (!includeReplicated)
            {
                forSeriesQuery = forSeriesQuery.Where(x => x.IsDownloaded);
            }

            var forSeries = forSeriesQuery.ToList();

            var seriesTitle = forSeries.Select(x => x.Manifest.SeriesTitle).FirstOrDefault(t => !string.IsNullOrEmpty(t));
            var externalMangaId = forSeries.Select(x => x.Manifest.ExternalMangaId).FirstOrDefault(id => !string.IsNullOrEmpty(id));

            var chapters = forSeries
                .GroupBy(x => x.Manifest.ChapterId)
                .Select(g =>
                {
                    var first = g.First().Manifest;
                    return new
                    {
                        chapterId = first.ChapterId,
                        chapterNumber = first.ChapterNumber,
                        volume = first.Volume,
                        title = first.Title,
                        uploadedAt = first.CreatedUtc,
                        manifests = g.Select(x => new
                        {
                            manifestHash = x.Hash.Value,
                            language = x.Manifest.Language,
                            scanGroup = x.Manifest.ScanGroup,
                            quality = x.Manifest.Quality,
                            uploadedAt = x.Manifest.CreatedUtc,
                            isDownloaded = x.IsDownloaded
                        }).ToList()
                    };
                })
                .OrderBy(c => c.chapterNumber)
                .ToList();

            return Ok(new { seriesId, seriesTitle, externalMangaId, chapters });
        }

        [HttpGet("blobs")]
        public ActionResult GetBlobs(
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 50)
        {
            var all = _blobStore.GetAllHashes().ToList();
            var total = all.Count;
            var items = all
                .Skip(offset)
                .Take(limit)
                .Select(h => new
                {
                    hash = h.Value,
                    sizeBytes = _blobStore.GetSize(h)
                })
                .ToList();

            return Ok(new { items, total, offset, limit });
        }
    }
}
