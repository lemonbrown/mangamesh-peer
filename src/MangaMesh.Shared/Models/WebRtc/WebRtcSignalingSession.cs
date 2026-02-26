namespace MangaMesh.Shared.Models.WebRtc
{
    public class WebRtcSignalingSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string OffererNodeId { get; set; } = string.Empty;
        public string AnswererNodeId { get; set; } = string.Empty;
        public string? SdpOffer { get; set; }
        public string? SdpAnswer { get; set; }
        public List<WebRtcIceCandidateDto> OffererCandidates { get; set; } = [];
        public List<WebRtcIceCandidateDto> AnswererCandidates { get; set; } = [];
        public WebRtcSignalingState State { get; set; } = WebRtcSignalingState.Pending;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
