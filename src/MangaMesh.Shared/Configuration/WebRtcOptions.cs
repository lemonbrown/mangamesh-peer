namespace MangaMesh.Shared.Configuration
{
    public class WebRtcOptions
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// STUN/TURN server URIs used for ICE candidate gathering.
        /// </summary>
        public string[] IceServers { get; set; } = ["stun:stun.l.google.com:19302"];

        /// <summary>
        /// Seconds before an incomplete signaling session is considered expired.
        /// </summary>
        public int SessionTimeoutSeconds { get; set; } = 120;

        /// <summary>
        /// Optional IP address to advertise as the host ICE candidate.
        /// Set this to the externally reachable IP when running behind Docker or NAT
        /// (e.g. "192.168.1.100" or the Docker host's LAN IP).
        /// When null, SIPSorcery auto-detects all local IPs.
        /// </summary>
        public string? AdvertisedIp { get; set; }

        /// <summary>
        /// Fixed UDP port for the WebRTC ICE transport.
        /// When non-zero the RTCPeerConnection binds to this port so it can be
        /// deterministically mapped in Docker (e.g. "49700:49700/udp").
        /// Must be set alongside AdvertisedIp for the injected host candidate to be useful.
        /// When zero, the OS assigns an ephemeral port.
        /// </summary>
        public int BindPort { get; set; } = 0;
    }
}
