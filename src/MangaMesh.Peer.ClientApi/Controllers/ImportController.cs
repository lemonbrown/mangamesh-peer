using MangaMesh.Peer.ClientApi.Models;
using MangaMesh.Peer.ClientApi.Services;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/import")]
    public class ImportController : ControllerBase
    {
        private readonly IImportChapterService _importer;
        private readonly ISeriesRegistry _seriesRegistry;
        private readonly ILogger<ImportController> _logger;
        private readonly string _inputDirectory = Path.Combine(AppContext.BaseDirectory, "input");
        private readonly string _importedChaptersFile;

        public ImportController(IImportChapterService importer, ISeriesRegistry seriesRegistry, ILogger<ImportController> logger)
        {
            _importer = importer;
            _seriesRegistry = seriesRegistry;
            _logger = logger;
            _importedChaptersFile = Path.Combine(_inputDirectory, "imported_chapters.json");
        }


        [HttpGet("chapters")]
        public async Task<ActionResult<IEnumerable<ImportChapterRequestDto>>> GetImportedChapters()
        {
            if (!System.IO.File.Exists(_importedChaptersFile))
            {
                return Ok(new List<ImportChapterRequestDto>());
            }

            var json = await System.IO.File.ReadAllTextAsync(_importedChaptersFile);
            var chapters = JsonSerializer.Deserialize<IEnumerable<ImportChapterRequestDto>>(json);

            return Ok(chapters);
        }

        [HttpGet("chapter-exists")]
        public async Task<ActionResult<bool>> CheckChapterExists(
            [FromQuery] ExternalMetadataSource source,
            [FromQuery] string externalMangaId,
            [FromQuery] double chapterNumber)
        {
            try
            {
                var (seriesId, _) = await _seriesRegistry.RegisterSeriesAsync(source, externalMangaId);
                var chapters = await _seriesRegistry.GetSeriesChaptersAsync(seriesId);
                return Ok(chapters.Any(c => c.ChapterNumber == chapterNumber));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CheckChapterExists failed for {Source}/{ExternalId} ch{Chapter}; defaulting to false",
                    source, externalMangaId, chapterNumber);
                // If the check fails (e.g. tracker down), let the main import flow try and report errors
                return Ok(false);
            }
        }

        [HttpPost("chapter")]
        public async Task<ActionResult<ImportResultDto>> ImportChapter(
            [FromBody] ImportChapterRequestDto request)
        {
            _logger.LogInformation("Import chapter request: series={SeriesId} ch={ChapterNumber} source={Source} externalId={ExternalMangaId} path={SourcePath}",
                request.SeriesId, request.ChapterNumber, request.Source, request.ExternalMangaId, request.SourcePath);

            ImportResultDto result;
            try
            {
                result = await _importer.ImportAsync(request);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Manifest already exists"))
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Import failed for series={SeriesId} ch={ChapterNumber}", request.SeriesId, request.ChapterNumber);
                return BadRequest("Import failed. Check server logs for details.");
            }

            if (!Directory.Exists(_inputDirectory))
            {
                Directory.CreateDirectory(_inputDirectory);
            }

            var importedChapters = new List<ImportChapterRequestDto>();
            if (System.IO.File.Exists(_importedChaptersFile))
            {
                var json = await System.IO.File.ReadAllTextAsync(_importedChaptersFile);
                importedChapters = JsonSerializer.Deserialize<List<ImportChapterRequestDto>>(json);
            }

            importedChapters.Add(request);
            var newJson = JsonSerializer.Serialize(importedChapters, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(_importedChaptersFile, newJson);

            return Ok(result);
        }

        [HttpPost("reannounce/{hash}")]
        public async Task<ActionResult> ReannounceManifest(string hash, [FromQuery] string nodeId = "mangamesh-node")
        {
            if (!ManifestHash.TryParse(hash, out var manifestHash))
            {
                return BadRequest("Invalid manifest hash format.");
            }

            try
            {
                await _importer.ReannounceAsync(manifestHash, nodeId);
                return Ok();
            }
            catch (FileNotFoundException)
            {
                return NotFound($"Manifest {hash} not found.");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("upload")]
        [RequestSizeLimit(524_288_000)] // 500 MB
        [RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
        public async Task<ActionResult<List<AnalyzedChapterDto>>> UploadChapter(
            [FromForm] IFormFileCollection files)
        {
            if (files == null || files.Count == 0)
                return BadRequest("No files uploaded.");

            // Create a unique batch ID
            var batchId = Guid.NewGuid().ToString("N");
            var batchPath = Path.Combine(_inputDirectory, "uploads", batchId);
            Directory.CreateDirectory(batchPath);

            var filePaths = new List<string>();

            var normalizedBatchPath = Path.GetFullPath(batchPath);

            // Save all files
            foreach (var file in files)
            {
                // Sanitize the path: normalize separators, strip leading slashes,
                // and remove any ".." or "." components to prevent directory traversal.
                var safeName = file.FileName.Replace('\\', '/').TrimStart('/');
                safeName = string.Join('/',
                    safeName.Split('/').Where(p => p != ".." && p != "." && p.Length > 0));

                var fullPath = Path.GetFullPath(Path.Combine(normalizedBatchPath, safeName));

                // Reject any path that escapes the batch directory
                if (!fullPath.StartsWith(normalizedBatchPath + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest($"Invalid file path rejected: {file.FileName}");
                }

                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Check for archive
                var ext = Path.GetExtension(fullPath).ToLowerInvariant();
                if (ext == ".zip" || ext == ".cbz")
                {
                    try
                    {
                        var extractPath = Path.Combine(Path.GetDirectoryName(fullPath)!, Path.GetFileNameWithoutExtension(fullPath));
                        Directory.CreateDirectory(extractPath);
                        System.IO.Compression.ZipFile.ExtractToDirectory(fullPath, extractPath);

                        // Add all extracted files to the list for analysis
                        filePaths.AddRange(Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract archive {File}; treating as regular file", fullPath);
                        filePaths.Add(fullPath);
                    }
                }
                else
                {
                    filePaths.Add(fullPath);
                }
            }

            // Analyze directory structure to identify "chapters"
            // We assume leaf directories containing images are chapters
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

            // Group files by their parent directory
            var chapters = filePaths
                .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .GroupBy(Path.GetDirectoryName)
                .Select(g =>
                {
                    var dirName = Path.GetFileName(g.Key!);

                    // Try to parse a number from the directory name
                    // Strategy: look for the last number in the string
                    var number = ExtractChapterNumber(dirName);

                    return new AnalyzedChapterDto(g.Key!, number);
                })
                .ToList();

            return Ok(chapters);
        }

        private static float ExtractChapterNumber(string name)
        {
            // Simple heuristic: find all numbers, take the last one.
            // "Chapter 1" -> 1
            // "Vol 2 Ch 15.5" -> 15.5
            try
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(name, @"\d+(\.\d+)?");
                if (matches.Count > 0)
                {
                    return float.Parse(matches[^1].Value);
                }
            }
            catch { }

            return 0;
        }

    }

    public record AnalyzedChapterDto(string SourcePath, float SuggestedChapterNumber);
}
