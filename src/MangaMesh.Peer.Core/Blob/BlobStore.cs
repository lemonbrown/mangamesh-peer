using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Replication;
using MangaMesh.Peer.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace MangaMesh.Peer.Core.Blob
{
    public sealed class BlobStore : IBlobStore
    {
        private readonly string _root;
        private readonly IStorageMonitorService _storageMonitor;
        private readonly IEvictionPolicy? _evictionPolicy;
        private readonly BlobStoreOptions _options;
        private readonly ILogger<BlobStore> _logger;

        public BlobStore(
            IOptions<BlobStoreOptions> options,
            IStorageMonitorService storageMonitor,
            ILogger<BlobStore> logger,
            IEvictionPolicy? evictionPolicy = null)
        {
            _options = options.Value;
            _root = _options.RootPath;
            _storageMonitor = storageMonitor;
            _evictionPolicy = evictionPolicy;
            _logger = logger;
            Directory.CreateDirectory(_root);
            _logger.LogInformation("BlobStore root: {Root}", _root);
        }

        public async Task<BlobHash> PutAsync(Stream data)
        {
            using var sha = SHA256.Create();
            using var temp = new MemoryStream();

            await data.CopyToAsync(temp);
            temp.Position = 0;

            var hashBytes = sha.ComputeHash(temp);
            var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            var blobHash = new BlobHash(hash);

            var path = GetPath(blobHash);
            if (File.Exists(path))
            {
                _logger.LogDebug("Blob {Hash} already exists, skipping write", hash);
                return blobHash;
            }

            // Attempt eviction before quota enforcement to free space proactively
            if (_evictionPolicy != null)
                await TryEvictAsync(temp.Length);

            await _storageMonitor.EnsureStorageAvailable(temp.Length);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            temp.Position = 0;
            var tmpPath = path + ".tmp";

            await using (var fs = File.Create(tmpPath))
                await temp.CopyToAsync(fs);

            File.Move(tmpPath, path, overwrite: false);

            _storageMonitor.NotifyBlobWritten(temp.Length);
            _logger.LogDebug("Stored blob {Hash} ({Bytes} bytes)", hash, temp.Length);

            return blobHash;
        }

        public async Task<Stream?> OpenReadAsync(BlobHash hash)
        {
            var path = GetPath(hash);
            if (!File.Exists(path))
                return null;

            return File.OpenRead(path);
        }

        public bool Exists(BlobHash hash)
            => File.Exists(GetPath(hash));

        public long GetSize(BlobHash hash)
        {
            var path = GetPath(hash);
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }

        public DateTime GetLastAccessed(BlobHash hash)
        {
            var path = GetPath(hash);
            return File.Exists(path) ? File.GetLastAccessTimeUtc(path) : DateTime.MinValue;
        }

        public void Delete(BlobHash hash)
        {
            var path = GetPath(hash);
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("Deleted blob {Hash}", hash.Value);
            }
        }

        public IEnumerable<BlobHash> GetAllHashes()
        {
            if (!Directory.Exists(_root))
                yield break;

            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);
                if (name.Length == 64 && !name.EndsWith(".tmp"))
                    yield return new BlobHash(name);
            }
        }

        private async Task TryEvictAsync(long bytesNeeded)
        {
            if (_evictionPolicy == null)
                return;

            var stats = await _storageMonitor.GetStorageStatsAsync();
            long usedBytes = (long)(stats.UsedMb * 1024 * 1024);
            long maxBytes = _options.MaxStorageBytes;

            long projected = usedBytes + bytesNeeded;
            if (projected <= maxBytes)
                return;

            long toFree = projected - maxBytes;
            _logger.LogInformation("Storage pressure: need to free {Bytes} bytes via eviction", toFree);

            await foreach (EvictionCandidate candidate in _evictionPolicy.GetEvictionCandidatesAsync(
                GetAllHashes(), GetSize, GetLastAccessed, toFree))
            {
                Delete(new BlobHash(candidate.BlobHash));
                toFree -= candidate.SizeBytes;
                if (toFree <= 0)
                    break;
            }
        }

        private string GetPath(BlobHash hash)
        {
            var a = hash.Value[..2];
            var b = hash.Value[2..4];
            return Path.Combine(_root, a, b, hash.Value);
        }
    }
}
