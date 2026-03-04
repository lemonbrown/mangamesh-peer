using MangaMesh.Peer.ClientApi.Services;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    /// <summary>
    /// Public (no auth) endpoint — allows other DHT peers to discover what this node is seeding.
    /// </summary>
    [ApiController]
    [Route("api/peer")]
    public class PeerCatalogController : ControllerBase
    {
        private readonly IManifestStore _manifestStore;
        private readonly SeriesCoverStore _coverStore;

        public PeerCatalogController(IManifestStore manifestStore, SeriesCoverStore coverStore)
        {
            _manifestStore = manifestStore;
            _coverStore = coverStore;
        }

        [HttpGet("manifest/{manifestHash}")]
        public async Task<IResult> GetManifest(string manifestHash)
        {
            var hash = new ManifestHash(manifestHash);
            var manifest = await _manifestStore.GetAsync(hash);
            if (manifest == null) return Results.NotFound();
            return Results.Json(manifest);
        }

        [HttpGet("catalog")]
        public async Task<IResult> GetCatalog()
        {
            var all = await _manifestStore.GetAllWithDataAsync();

            var series = all
                .GroupBy(x => x.Manifest.SeriesId)
                .Select(g => new
                {
                    SeriesId = g.Key,
                    HasCover = _coverStore.HasCover(g.Key),
                    Chapters = g.OrderBy(x => x.Manifest.ChapterNumber)
                        .Select(x => new
                        {
                            ChapterId = x.Manifest.ChapterId,
                            ManifestHash = x.Hash.Value,
                            Title = x.Manifest.Title,
                            ChapterNumber = x.Manifest.ChapterNumber,
                            Language = x.Manifest.Language,
                            ScanGroup = x.Manifest.ScanGroup,
                            Quality = x.Manifest.Quality,
                            CreatedUtc = x.Manifest.CreatedUtc,
                        })
                        .ToList()
                })
                .OrderBy(s => s.SeriesId)
                .ToList();

            return Results.Ok(new { Series = series });
        }

        [HttpGet("cover/{seriesId}")]
        public IResult GetCover(string seriesId)
        {
            var stream = _coverStore.OpenRead(seriesId);
            if (stream == null) return Results.NotFound();
            return Results.Stream(stream, _coverStore.GetContentType(seriesId));
        }
    }
}
