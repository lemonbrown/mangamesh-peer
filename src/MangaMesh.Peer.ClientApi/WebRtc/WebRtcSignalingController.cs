using MangaMesh.Peer.ClientApi.WebRtc;
using MangaMesh.Shared.Models.WebRtc;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [ApiController]
    [Route("api/webrtc")]
    public class WebRtcSignalingController : ControllerBase
    {
        private readonly ClientWebRtcService _webRtcService;

        public WebRtcSignalingController(ClientWebRtcService webRtcService)
        {
            _webRtcService = webRtcService;
        }

        /// <summary>
        /// Browser posts its SDP offer. Peer returns its SDP answer and initial ICE candidates.
        /// </summary>
        [HttpPost("offer")]
        public async Task<IActionResult> ReceiveOffer([FromBody] BrowserOfferRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Sdp))
                return BadRequest("Sdp is required.");

            WebRtcAnswer answer = await _webRtcService.HandleBrowserOfferAsync(request.Sdp, cancellationToken);
            return Ok(answer);
        }

        /// <summary>
        /// Browser submits its ICE candidates (trickle ICE).
        /// </summary>
        [HttpPost("{sessionId}/ice")]
        public IActionResult AddIceCandidate(string sessionId, [FromBody] WebRtcIceCandidateDto candidate)
        {
            candidate.SessionId = sessionId;
            _webRtcService.AddBrowserIceCandidate(sessionId, candidate);
            return NoContent();
        }

        /// <summary>
        /// Browser polls for the peer's ICE candidates (trickle ICE).
        /// </summary>
        [HttpGet("{sessionId}/ice")]
        public IActionResult GetIceCandidates(string sessionId)
        {
            List<WebRtcIceCandidateDto> candidates = _webRtcService.GetServerCandidates(sessionId);
            return Ok(candidates);
        }
    }

    public class BrowserOfferRequest
    {
        public string Sdp { get; set; } = string.Empty;
    }
}
