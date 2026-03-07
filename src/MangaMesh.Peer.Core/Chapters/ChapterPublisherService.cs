using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;

namespace MangaMesh.Peer.Core.Chapters
{
    public class ChapterPublisherService : IChapterPublisherService
    {
        private readonly IKeyStore _keyStore;
        private readonly INodeIdentity _nodeIdentity;
        private readonly IManifestStore _manifestStore;
        private readonly IManifestSigningService _manifestSigning;
        private readonly ITrackerPublisher _trackerPublisher;
        private readonly IDhtNode? _dhtNode;
        private readonly ILogger<ChapterPublisherService>? _logger;

        public ChapterPublisherService(
            IKeyStore keyStore,
            INodeIdentity nodeIdentity,
            IManifestStore manifestStore,
            IManifestSigningService manifestSigning,
            ITrackerPublisher trackerPublisher,
            IDhtNode? dhtNode = null,
            ILogger<ChapterPublisherService>? logger = null)
        {
            _keyStore = keyStore;
            _nodeIdentity = nodeIdentity;
            _manifestStore = manifestStore;
            _manifestSigning = manifestSigning;
            _trackerPublisher = trackerPublisher;
            _dhtNode = dhtNode;
            _logger = logger;
        }

        public async Task<(ManifestHash Hash, bool AlreadyExists)> PublishChapterAsync(
            ImportChapterRequest request,
            string seriesId,
            string seriesTitle,
            List<ChapterFileEntry> entries,
            long totalSize,
            CancellationToken ct = default)
        {
            var keyPair = await _keyStore.GetAsync();

            // Derive public key from private key to ensure they match for signing
            byte[] privateKeyBytes = Convert.FromBase64String(keyPair.PrivateKeyBase64);
            var key = Key.Import(SignatureAlgorithm.Ed25519, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
            var derivedPublicKey = Convert.ToBase64String(key.PublicKey.Export(KeyBlobFormat.RawPublicKey));

            var title = request.DisplayName;
            if (!string.IsNullOrEmpty(seriesTitle) && !title.Contains(seriesTitle, StringComparison.OrdinalIgnoreCase))
            {
                if (title.Contains(request.ExternalMangaId))
                    title = title.Replace(request.ExternalMangaId, seriesTitle);
                else
                    title = $"{seriesTitle} {title}";
            }

            ChapterManifest chapterManifest = new()
            {
                SchemaVersion = 2,
                ChapterNumber = request.ChapterNumber,
                CreatedUtc = new DateTime(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc),
                ChapterId = seriesId + ":" + request.ChapterNumber.ToString() + ":" + request.Language,
                Language = request.Language,
                SeriesId = seriesId,
                SeriesTitle = string.IsNullOrEmpty(seriesTitle) ? null : seriesTitle,
                ExternalMangaId = string.IsNullOrEmpty(request.ExternalMangaId) ? null : request.ExternalMangaId,
                ScanGroup = request.ScanlatorId,
                Title = title,
                TotalSize = totalSize,
                PublicKey = derivedPublicKey,
                SignedBy = "self",
                Quality = request.Quality,
                Files = entries
            };

            var hash = ManifestHash.FromManifest(chapterManifest);

            var isManifestExisting = await _manifestStore.ExistsAsync(hash);
            if (isManifestExisting)
                return (hash, true);

            // Save unsigned manifest first
            await _manifestStore.SaveAsync(hash, chapterManifest, isDownloaded: true);

            // Sign manifest
            var signedManifest = _manifestSigning.SignManifest(chapterManifest, key);

            // Publish — challenge-response auth handled by TrackerPublisher
            var announceRequest = new Shared.Models.AnnounceManifestRequest
            {
                NodeId = Convert.ToHexString(_nodeIdentity.NodeId).ToLowerInvariant(),
                ManifestHash = hash,
                SchemaVersion = chapterManifest.SchemaVersion,
                SeriesId = chapterManifest.SeriesId,
                ChapterNumber = chapterManifest.ChapterNumber,
                Language = chapterManifest.Language,
                ReleaseType = request.ReleaseType,
                Quality = request.Quality,
                Source = request.Source,
                ExternalMangaId = request.ExternalMangaId,
                ChapterId = chapterManifest.ChapterId,
                Title = chapterManifest.Title,
                ScanGroup = chapterManifest.ScanGroup,
                TotalSize = chapterManifest.TotalSize,
                CreatedUtc = chapterManifest.CreatedUtc,
                Signature = signedManifest.Signature,
                PublicKey = signedManifest.PublisherPublicKey,
                SignedBy = chapterManifest.SignedBy,
                Files = (List<Shared.Models.ChapterFileEntry>)chapterManifest.Files
            };

            try
            {
                await _trackerPublisher.PublishManifestAsync(announceRequest, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Tracker manifest publish failed for {Hash}; chapter stored locally only", hash.Value);
            }

            // Re-save with signature
            chapterManifest = chapterManifest with { Signature = signedManifest.Signature };
            await _manifestStore.SaveAsync(hash, chapterManifest, isDownloaded: true);

            // Announce to DHT so the gateway can discover this node as a provider
            if (_dhtNode != null)
            {
                await _dhtNode.StoreAsync(Convert.FromHexString(hash.Value));
            }

            return (hash, false);
        }

        public async Task ReannounceAsync(ManifestHash hash, string nodeId)
        {
            var manifest = await _manifestStore.GetAsync(hash);
            if (manifest == null)
                throw new FileNotFoundException($"Manifest {hash} not found");

            if (string.IsNullOrEmpty(manifest.Signature) || string.IsNullOrEmpty(manifest.PublicKey))
                throw new InvalidOperationException("Manifest does not contain signature data. Cannot re-announce.");

            await _trackerPublisher.PublishManifestAsync(new Shared.Models.AnnounceManifestRequest
            {
                NodeId = nodeId,
                ManifestHash = hash,
                SchemaVersion = manifest.SchemaVersion,
                SeriesId = manifest.SeriesId,
                ChapterNumber = manifest.ChapterNumber,
                Language = manifest.Language,
                ReleaseType = ReleaseType.VerifiedScanlation,
                ChapterId = manifest.ChapterId,
                Title = manifest.Title,
                ScanGroup = manifest.ScanGroup,
                TotalSize = manifest.TotalSize,
                CreatedUtc = manifest.CreatedUtc,
                Signature = manifest.Signature,
                PublicKey = manifest.PublicKey,
                Files = (List<Shared.Models.ChapterFileEntry>)manifest.Files
            });
        }
    }
}
