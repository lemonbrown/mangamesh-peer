namespace MangaMesh.Shared.Models.WebRtc
{
    public class WebRtcAnswer
    {
        public string SessionId { get; set; } = string.Empty;
        public string Sdp { get; set; } = string.Empty;
        public List<WebRtcIceCandidateDto> GatheredCandidates { get; set; } = [];
    }
}
