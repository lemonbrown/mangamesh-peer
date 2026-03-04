using MangaMesh.Peer.ClientApi.Services;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class BroadcastsController : ControllerBase
    {
        private readonly IDhtNode _dhtNode;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISeriesRegistry _seriesRegistry;
        private readonly SeriesCoverStore _coverStore;

        public BroadcastsController(IDhtNode dhtNode, IHttpClientFactory httpClientFactory, ISeriesRegistry seriesRegistry, SeriesCoverStore coverStore)
        {
            _dhtNode = dhtNode;
            _httpClientFactory = httpClientFactory;
            _seriesRegistry = seriesRegistry;
            _coverStore = coverStore;
        }

        private record PeerCatalog(string NodeId, string Host, int HttpApiPort, List<PeerSeriesEntry> Series);
        private record PeerSeriesEntry(string SeriesId, bool HasCover, List<PeerChapterEntry> Chapters);
        private record PeerChapterEntry(string ChapterId, string ManifestHash, string Title, double ChapterNumber, string Language, string ScanGroup, string Quality, DateTime CreatedUtc);

        [HttpGet]
        public async Task<IResult> GetBroadcasts(CancellationToken cancellationToken)
        {
            var entries = _dhtNode.RoutingTable.GetAll()
                .Where(e => e.NodeId != null && e.Address != null && e.Address.HttpApiPort > 0)
                .ToList();

            // Fetch catalog from each peer
            var catalogTasks = entries.Select(async entry =>
            {
                try
                {
                    var nodeId = Convert.ToHexString(entry.NodeId).ToLowerInvariant();
                    var host = entry.Address.Host;
                    var httpApiPort = entry.Address.HttpApiPort;

                    var client = _httpClientFactory.CreateClient("PeerCatalog");
                    using var response = await client.GetAsync($"http://{host}:{httpApiPort}/api/peer/catalog", cancellationToken);
                    if (!response.IsSuccessStatusCode) return null;

                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    var doc = JsonDocument.Parse(body);

                    var series = new List<PeerSeriesEntry>();
                    if (doc.RootElement.TryGetProperty("series", out var seriesEl) ||
                        doc.RootElement.TryGetProperty("Series", out seriesEl))
                    {
                        foreach (var item in seriesEl.EnumerateArray())
                        {
                            var seriesId = item.TryGetProperty("seriesId", out var sid) ? sid.GetString()
                                : item.TryGetProperty("SeriesId", out var Sid) ? Sid.GetString() : null;
                            if (seriesId == null) continue;

                            var hasCover = (item.TryGetProperty("hasCover", out var hc) || item.TryGetProperty("HasCover", out hc))
                                && hc.ValueKind == JsonValueKind.True;

                            var chaptersEl = item.TryGetProperty("chapters", out var cEl) ? cEl
                                : item.TryGetProperty("Chapters", out var CEl) ? CEl
                                : (JsonElement?)null;

                            var chapters = new List<PeerChapterEntry>();
                            if (chaptersEl.HasValue)
                            {
                                foreach (var ch in chaptersEl.Value.EnumerateArray())
                                {
                                    var chapterId = ch.TryGetProperty("chapterId", out var cid) ? cid.GetString()
                                        : ch.TryGetProperty("ChapterId", out var Cid) ? Cid.GetString() : null;
                                    var manifestHash = ch.TryGetProperty("manifestHash", out var mh) ? mh.GetString()
                                        : ch.TryGetProperty("ManifestHash", out var Mh) ? Mh.GetString() : null;
                                    var title = ch.TryGetProperty("title", out var t) ? t.GetString()
                                        : ch.TryGetProperty("Title", out var T) ? T.GetString() : null;
                                    var chapterNumber = ch.TryGetProperty("chapterNumber", out var cn) ? cn.GetDouble()
                                        : ch.TryGetProperty("ChapterNumber", out var Cn) ? Cn.GetDouble() : 0.0;
                                    var language = ch.TryGetProperty("language", out var lang) ? lang.GetString()
                                        : ch.TryGetProperty("Language", out var Lang) ? Lang.GetString() : null;
                                    var scanGroup = ch.TryGetProperty("scanGroup", out var sg) ? sg.GetString()
                                        : ch.TryGetProperty("ScanGroup", out var Sg) ? Sg.GetString() : null;
                                    var quality = ch.TryGetProperty("quality", out var q) ? q.GetString()
                                        : ch.TryGetProperty("Quality", out var Q) ? Q.GetString() : null;
                                    var createdUtc = ch.TryGetProperty("createdUtc", out var cu) ? cu.GetDateTime()
                                        : ch.TryGetProperty("CreatedUtc", out var Cu) ? Cu.GetDateTime() : DateTime.MinValue;

                                    if (chapterId != null)
                                        chapters.Add(new PeerChapterEntry(chapterId, manifestHash ?? "", title ?? "", chapterNumber, language ?? "", scanGroup ?? "", quality ?? "", createdUtc));
                                }
                            }

                            series.Add(new PeerSeriesEntry(seriesId, hasCover, chapters));
                        }
                    }

                    return new PeerCatalog(nodeId, host, httpApiPort, series);
                }
                catch { return null; }
            });

            var catalogs = (await Task.WhenAll(catalogTasks)).Where(c => c != null).Select(c => c!).ToList();

            // Download covers from remote peers into local store (fire-and-forget, one per series)
            var queued = new HashSet<string>();
            foreach (var catalog in catalogs)
            {
                foreach (var s in catalog.Series.Where(s => s.HasCover))
                {
                    if (!_coverStore.HasCover(s.SeriesId) && queued.Add(s.SeriesId))
                        _ = TryFetchCoverFromPeerAsync(catalog.Host, catalog.HttpApiPort, s.SeriesId);
                }
            }

            // Batch-resolve series titles from tracker
            var allSeriesIds = catalogs.SelectMany(c => c.Series.Select(s => s.SeriesId)).Distinct().ToArray();
            var titleMap = new Dictionary<string, string>();
            var externalMangaIdMap = new Dictionary<string, string>();
            if (allSeriesIds.Length > 0)
            {
                try
                {
                    var summaries = await _seriesRegistry.SearchSeriesAsync("", ids: allSeriesIds);
                    foreach (var s in summaries)
                    {
                        titleMap[s.SeriesId] = s.Title;
                        if (!string.IsNullOrEmpty(s.ExternalMangaId))
                            externalMangaIdMap[s.SeriesId] = s.ExternalMangaId;
                    }
                }
                catch { /* tracker offline — fall back to SeriesId */ }
            }

            var broadcasts = catalogs.Select(c => new
            {
                NodeId = c.NodeId,
                Host = c.Host,
                HttpApiPort = c.HttpApiPort,
                Series = c.Series.Select(s => new
                {
                    SeriesId = s.SeriesId,
                    SeriesTitle = titleMap.TryGetValue(s.SeriesId, out var title) ? title : null,
                    ExternalMangaId = externalMangaIdMap.TryGetValue(s.SeriesId, out var extId) ? extId : null,
                    Chapters = s.Chapters.Select(ch => new
                    {
                        ChapterId = ch.ChapterId,
                        ManifestHash = ch.ManifestHash,
                        NodeId = c.NodeId,
                        Title = ch.Title,
                        ChapterNumber = ch.ChapterNumber,
                        Language = ch.Language,
                        ScanGroup = ch.ScanGroup,
                        Quality = ch.Quality,
                        CreatedUtc = ch.CreatedUtc,
                    }).ToList()
                }).ToList()
            }).ToList();

            return Results.Ok(broadcasts);
        }

        [HttpGet("peek")]
        public async Task<IResult> PeekChapter(string nodeId, string manifestHash, CancellationToken cancellationToken)
        {
            // Resolve host:port from DHT routing table
            byte[] nodeIdBytes;
            try { nodeIdBytes = Convert.FromHexString(nodeId); }
            catch { return Results.BadRequest("Invalid nodeId"); }

            var address = _dhtNode.RoutingTable.GetAddressForNode(nodeIdBytes);
            if (address == null || address.HttpApiPort == 0)
                return Results.NotFound("Node not found in routing table or has no HTTP API port");

            var baseUrl = $"http://{address.Host}:{address.HttpApiPort}";
            var client = _httpClientFactory.CreateClient("PeerCatalog");

            // Fetch manifest from peer
            ChapterManifest? manifest;
            try
            {
                using var manifestResponse = await client.GetAsync($"{baseUrl}/api/peer/manifest/{Uri.EscapeDataString(manifestHash)}", cancellationToken);
                if (!manifestResponse.IsSuccessStatusCode)
                    return Results.NotFound("Manifest not found on peer");
                manifest = await manifestResponse.Content.ReadFromJsonAsync<ChapterManifest>(cancellationToken: cancellationToken);
                if (manifest == null || manifest.Files.Count == 0)
                    return Results.NotFound("Manifest is empty");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to fetch manifest: {ex.Message}");
            }

            // Pick a random page
            var rng = new Random();
            var file = manifest.Files[rng.Next(manifest.Files.Count)];

            // Fetch PageManifest blob from peer
            PageManifest? pageManifest;
            try
            {
                using var pageResponse = await client.GetAsync($"{baseUrl}/api/blob/{Uri.EscapeDataString(file.Hash)}", cancellationToken);
                if (!pageResponse.IsSuccessStatusCode)
                    return Results.NotFound("Page manifest blob not found on peer");
                pageManifest = await pageResponse.Content.ReadFromJsonAsync<PageManifest>(cancellationToken: cancellationToken);
                if (pageManifest == null || pageManifest.Chunks.Count == 0)
                    return Results.NotFound("Page manifest is empty");
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to fetch page manifest: {ex.Message}");
            }

            // Assemble chunks into full image (ephemeral — not stored locally)
            var fileData = new byte[pageManifest.FileSize];
            int offset = 0;
            try
            {
                foreach (var chunkHash in pageManifest.Chunks)
                {
                    using var chunkResponse = await client.GetAsync($"{baseUrl}/api/blob/{Uri.EscapeDataString(chunkHash)}", cancellationToken);
                    if (!chunkResponse.IsSuccessStatusCode)
                        return Results.NotFound($"Chunk {chunkHash} not found on peer");
                    var chunkBytes = await chunkResponse.Content.ReadAsByteArrayAsync(cancellationToken);
                    chunkBytes.CopyTo(fileData, offset);
                    offset += chunkBytes.Length;
                }
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to fetch chunk: {ex.Message}");
            }

            return Results.Bytes(fileData, pageManifest.MimeType);
        }

        private async Task TryFetchCoverFromPeerAsync(string host, int port, string seriesId)
        {
            if (_coverStore.HasCover(seriesId)) return;
            try
            {
                var client = _httpClientFactory.CreateClient("PeerCatalog");
                using var response = await client.GetAsync($"http://{host}:{port}/api/peer/cover/{Uri.EscapeDataString(seriesId)}");
                if (!response.IsSuccessStatusCode) return;

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                var ext = contentType switch
                {
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    _ => ".jpg"
                };

                using var stream = await response.Content.ReadAsStreamAsync();
                await _coverStore.SaveAsync(seriesId, stream, ext);
            }
            catch { /* best-effort */ }
        }
    }
}
