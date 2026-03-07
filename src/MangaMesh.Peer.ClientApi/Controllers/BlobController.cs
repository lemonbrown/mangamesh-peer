using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class BlobController : ControllerBase
    {
        private readonly ILogger<BlobController> _logger;
        private readonly IBlobStore _blobStore;
        private readonly IPeerFetcher _peerFetcher;
        private readonly ISourceProviderCache _providerCache;

        public BlobController(
            ILogger<BlobController> logger,
            IBlobStore blobStore,
            IPeerFetcher peerFetcher,
            ISourceProviderCache providerCache)
        {
            _logger = logger;
            _blobStore = blobStore;
            _peerFetcher = peerFetcher;
            _providerCache = providerCache;
        }

        [HttpGet("{hash}", Name = "GetBlobByHash")]
        public async Task<IResult> GetByHashAsync(string hash)
        {
            var blobHash = new BlobHash(hash);

            if (_blobStore.Exists(blobHash))
            {
                var stream = await _blobStore.OpenReadAsync(blobHash);
                return Results.Stream(stream, "application/octet-stream");
            }

            var source = _providerCache.GetSource(hash);
            if (source != null)
            {
                var blobHash2 = new BlobHash(hash);
                var data = await ReadBytesAsync(blobHash2, source);
                if (data != null)
                    return Results.Bytes(data, "application/octet-stream");
            }

            return Results.NotFound();
        }

        /// <summary>
        /// Reassembles a file from a PageManifest. Reads page manifest and image chunks from
        /// local storage or fetches them on demand from the source peer.
        /// Fetched blobs are cached locally (fire-and-forget) so this peer becomes a replica.
        /// </summary>
        [HttpGet("~/api/file/{pageHash}", Name = "GetFileByPageHash")]
        public async Task<IActionResult> GetFileByPageHashAsync(string pageHash)
        {
            var manifestHash = new BlobHash(pageHash);

            // ── 1. Get page manifest bytes (local store or proxy) ──────────────
            byte[]? pageManifestBytes = await ReadBytesAsync(manifestHash);
            if (pageManifestBytes == null)
                return NotFound();

            PageManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<PageManifest>(pageManifestBytes);
                if (manifest == null) return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize PageManifest for {PageHash}", pageHash);
                return BadRequest("Invalid PageManifest.");
            }

            if (manifest.FileSize > 100 * 1024 * 1024)
                return BadRequest("File too large for in-memory reassembly.");

            // ── 2. Assemble image from chunks (local store or proxy) ───────────
            var fileData = new byte[manifest.FileSize];
            int offset = 0;

            // The page manifest source is also the source for all its chunks.
            var chunkSource = _providerCache.GetSource(pageHash);

            // Register chunk hashes against the same source so raw blob requests work too.
            if (chunkSource != null)
                _providerCache.RegisterSources(manifest.Chunks, chunkSource);

            foreach (var chunkHash in manifest.Chunks)
            {
                var chunkBytes = await ReadBytesAsync(new BlobHash(chunkHash), chunkSource);
                if (chunkBytes == null)
                {
                    _logger.LogWarning("Missing chunk {ChunkHash} for page {PageHash}", chunkHash, pageHash);
                    return NotFound($"Missing chunk {chunkHash}.");
                }

                if (offset + chunkBytes.Length > fileData.Length)
                    return BadRequest("Chunk data exceeds declared file size.");

                chunkBytes.CopyTo(fileData, offset);
                offset += chunkBytes.Length;
            }

            return File(fileData, manifest.MimeType);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns blob bytes from local storage, falling back to proxy fetch.
        /// Proxy-fetched data is cached locally (fire-and-forget) so the reader
        /// accumulates replicas of what they read.
        /// </summary>
        private async Task<byte[]?> ReadBytesAsync(BlobHash hash,
            MangaMesh.Peer.Core.Transport.NodeAddress? knownSource = null)
        {
            if (_blobStore.Exists(hash))
            {
                await using var stream = await _blobStore.OpenReadAsync(hash);
                if (stream == null) return null;
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                return ms.ToArray();
            }

            var source = knownSource ?? _providerCache.GetSource(hash.Value);
            if (source == null) return null;

            var data = await _peerFetcher.FetchBlobForProxyAsync(source, hash.Value);

            if (data != null)
            {
                // Cache locally so this peer becomes a replica for what it reads.
                // Fire-and-forget — don't block the serving response on storage I/O.
                _ = Task.Run(async () =>
                {
                    try { await _blobStore.PutAsync(new MemoryStream(data)); }
                    catch (Exception ex) { _logger.LogDebug(ex, "Cache-on-read store failed for {Hash}", hash.Value[..8]); }
                });
            }

            return data;
        }
    }
}
