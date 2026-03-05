namespace MangaMesh.Peer.Core.Replication;

public interface IPeerStorageProfileProvider
{
    /// <summary>Returns the local node's current storage profile (cached, refresh every 30s).</summary>
    PeerStorageProfile GetLocalProfile();

    /// <summary>Updates the uptime score (0–100) based on recent connectivity observations.</summary>
    void UpdateUptimeScore(byte score);
}
