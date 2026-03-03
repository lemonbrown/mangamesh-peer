using MangaMesh.Peer.Core.Configuration;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.ClientApi.Services
{
    /// <summary>
    /// Stores per-series cover images as files under {blobRoot}/covers/{seriesId}.jpg.
    /// </summary>
    public sealed class SeriesCoverStore
    {
        private readonly string _coversDir;
        private static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".webp"];

        public SeriesCoverStore(IOptions<BlobStoreOptions> blobOptions)
        {
            _coversDir = Path.Combine(blobOptions.Value.RootPath, "covers");
            Directory.CreateDirectory(_coversDir);
        }

        public bool HasCover(string seriesId) => FindPath(seriesId) != null;

        public async Task SaveAsync(string seriesId, Stream data, string extension = ".jpg")
        {
            var path = Path.Combine(_coversDir, seriesId + extension);
            using var file = File.Create(path);
            await data.CopyToAsync(file);
        }

        public Stream? OpenRead(string seriesId)
        {
            var path = FindPath(seriesId);
            return path != null ? File.OpenRead(path) : null;
        }

        public string GetContentType(string seriesId)
        {
            var path = FindPath(seriesId);
            return Path.GetExtension(path ?? "").ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }

        private string? FindPath(string seriesId)
        {
            foreach (var ext in Extensions)
            {
                var path = Path.Combine(_coversDir, seriesId + ext);
                if (File.Exists(path)) return path;
            }
            return null;
        }
    }
}
