namespace MangaMesh.Shared.Models.WebRtc
{
    public class WebRtcIceCandidateDto
    {
        public string SessionId { get; set; } = string.Empty;

        /// <summary>"offerer" or "answerer"</summary>
        public string Side { get; set; } = string.Empty;

        public string Candidate { get; set; } = string.Empty;
        public string? SdpMid { get; set; }
        public int? SdpMLineIndex { get; set; }
    }
}
