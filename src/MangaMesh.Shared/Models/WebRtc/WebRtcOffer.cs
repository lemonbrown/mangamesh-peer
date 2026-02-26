namespace MangaMesh.Shared.Models.WebRtc
{
    public class WebRtcOffer
    {
        public string SessionId { get; set; } = string.Empty;
        public string OffererNodeId { get; set; } = string.Empty;
        public string AnswererNodeId { get; set; } = string.Empty;
        public string Sdp { get; set; } = string.Empty;
    }
}
