namespace MangaMesh.Peer.Core.Replication;

public sealed record PeerStorageProfile(
    string PeerId,
    long StorageCapacityBytes,
    long StorageUsedBytes,
    BandwidthClass BandwidthClass,
    byte UptimeScore,       // 0–100
    bool IsSuperSeeder,
    DateTime MeasuredAtUtc
)
{
    public long FreeStorageBytes => Math.Max(0L, StorageCapacityBytes - StorageUsedBytes);

    public double UtilizationRatio =>
        StorageCapacityBytes == 0 ? 1.0 : (double)StorageUsedBytes / StorageCapacityBytes;

    public static PeerStorageProfile Unknown(string peerId) =>
        new(peerId, 0, 0, BandwidthClass.Low, 0, false, DateTime.UtcNow);
}
