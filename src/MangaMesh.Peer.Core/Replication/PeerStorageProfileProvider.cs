using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Node;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Core.Replication;

/// <summary>
/// Reads local disk usage from <see cref="IBlobStore"/> to build a <see cref="PeerStorageProfile"/>
/// for gossip. Results are cached for 30 seconds to avoid repeated directory scans.
/// Uses IServiceScopeFactory to safely consume the scoped IBlobStore from this singleton.
/// </summary>
public sealed class PeerStorageProfileProvider : IPeerStorageProfileProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INodeIdentity _identity;
    private readonly BlobStoreOptions _storeOpts;
    private readonly ReplicationOptions _replOpts;

    private PeerStorageProfile? _cache;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private byte _uptimeScore = 80; // default reasonable uptime

    public PeerStorageProfileProvider(
        IServiceScopeFactory scopeFactory,
        INodeIdentity identity,
        IOptions<BlobStoreOptions> storeOptions,
        IOptions<ReplicationOptions> replOptions)
    {
        _scopeFactory = scopeFactory;
        _identity = identity;
        _storeOpts = storeOptions.Value;
        _replOpts = replOptions.Value;
    }

    public PeerStorageProfile GetLocalProfile()
    {
        if (_cache != null && DateTime.UtcNow < _cacheExpiry)
            return _cache;

        long usedBytes;
        using (var scope = _scopeFactory.CreateScope())
        {
            var blobStore = scope.ServiceProvider.GetRequiredService<IBlobStore>();
            usedBytes = blobStore.GetAllHashes().Sum(h => blobStore.GetSize(h));
        }

        string peerId = Convert.ToHexString(_identity.NodeId).ToLowerInvariant();

        _cache = new PeerStorageProfile(
            PeerId: peerId,
            StorageCapacityBytes: _storeOpts.MaxStorageBytes,
            StorageUsedBytes: usedBytes,
            BandwidthClass: _replOpts.BandwidthClass,
            UptimeScore: _uptimeScore,
            IsSuperSeeder: _replOpts.IsSuperSeeder,
            MeasuredAtUtc: DateTime.UtcNow
        );

        _cacheExpiry = DateTime.UtcNow.AddSeconds(30);
        return _cache;
    }

    public void UpdateUptimeScore(byte score)
    {
        _uptimeScore = score;
        _cache = null; // invalidate so next call picks up new score
    }
}
