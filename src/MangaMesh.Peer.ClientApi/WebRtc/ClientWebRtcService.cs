using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Shared.Configuration;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Models.WebRtc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIPSorcery.Net;

namespace MangaMesh.Peer.ClientApi.WebRtc
{
    /// <summary>
    /// Manages WebRTC browser sessions for the ClientApi peer node.
    /// Mirrors GatewayWebRtcService — browsers connect directly to a peer node
    /// to fetch manifests and blobs over a WebRTC DataChannel.
    /// </summary>
    public class ClientWebRtcService
    {
        private record BrowserSession(
            string SessionId,
            RTCPeerConnection PeerConnection,
            List<WebRtcIceCandidateDto> ServerCandidates,
            DateTime CreatedAtUtc);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly WebRtcOptions _options;
        private readonly ILogger<ClientWebRtcService> _logger;

        private readonly ConcurrentDictionary<string, BrowserSession> _sessions = new();

        public ClientWebRtcService(
            IServiceScopeFactory scopeFactory,
            IOptions<WebRtcOptions> options,
            ILogger<ClientWebRtcService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        private void CleanupSession(string sessionId)
        {
            if (_sessions.TryRemove(sessionId, out BrowserSession? old))
            {
                try { old.PeerConnection.Dispose(); } catch { }
                _logger.LogInformation("[WebRTC-Browser] Peer session {SessionId} cleaned up", sessionId);
            }
        }

        public async Task<WebRtcAnswer> HandleBrowserOfferAsync(string offerSdp, CancellationToken cancellationToken = default)
        {
            // When BindPort is fixed, at most one RTCPeerConnection can hold that port.
            // Close all existing sessions synchronously before creating the new one,
            // so the old UDP socket is released before the new bind attempt.
            if (_options.BindPort > 0)
            {
                foreach (var key in _sessions.Keys.ToList())
                    CleanupSession(key);
            }
            else
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-5);
                foreach (var kvp in _sessions.Where(s => s.Value.CreatedAtUtc < cutoff).ToList())
                    CleanupSession(kvp.Key);
            }

            string sessionId = Guid.NewGuid().ToString();

            // When AdvertisedIp is set (Docker/NAT mode) we inject a synthetic host candidate,
            // so STUN-derived srflx candidates are useless (Docker NAT makes them malformed).
            // Skip STUN servers to avoid the ~10 s gathering wait.
            var iceServers = string.IsNullOrEmpty(_options.AdvertisedIp)
                ? _options.IceServers.Select(uri => new RTCIceServer { urls = uri }).ToList()
                : [];

            var rtcConfig = new RTCConfiguration { iceServers = iceServers };
            var pc = _options.BindPort > 0
                ? new RTCPeerConnection(rtcConfig, _options.BindPort)
                : new RTCPeerConnection(rtcConfig);

            pc.onconnectionstatechange += state =>
            {
                if (state == RTCPeerConnectionState.closed ||
                    state == RTCPeerConnectionState.failed ||
                    state == RTCPeerConnectionState.disconnected)
                {
                    CleanupSession(sessionId);
                }
            };

            var serverCandidates = new List<WebRtcIceCandidateDto>();
            var iceGatheringComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            pc.onicecandidate += candidate =>
            {
                if (candidate == null)
                    iceGatheringComplete.TrySetResult();
                else
                    serverCandidates.Add(new WebRtcIceCandidateDto
                    {
                        SessionId = sessionId,
                        Side = "answerer",
                        Candidate = candidate.candidate,
                        SdpMid = candidate.sdpMid,
                        SdpMLineIndex = candidate.sdpMLineIndex
                    });
            };

            pc.ondatachannel += receivedDc =>
            {
                receivedDc.onmessage += (_, _, data) =>
                    _ = HandleDataChannelMessageAsync(sessionId, receivedDc, data);
            };

            pc.setRemoteDescription(new RTCSessionDescriptionInit
            {
                sdp = offerSdp,
                type = RTCSdpType.offer
            });

            var answer = pc.createAnswer();
            pc.setLocalDescription(answer);

            await Task.WhenAny(iceGatheringComplete.Task, Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));

            // When running in Docker, inject a synthetic host candidate for the advertised (host-reachable) IP.
            // SIPSorcery may bind to BindPort+2 (it reserves a port pair for RTP/RTCP), so we read the
            // actual bound port from the gathered candidates rather than trusting BindPort directly.
            // Docker must map the entire small range (e.g. 49700-49710:49700-49710/udp) to ensure the
            // actual port is reachable on the host.
            if (!string.IsNullOrEmpty(_options.AdvertisedIp))
            {
                var hostCand = serverCandidates.FirstOrDefault(c => c.Candidate.Contains("typ host"));
                if (hostCand != null)
                {
                    // ICE candidate format: <foundation> <component> <transport> <priority> <ip> <port> typ <type> ...
                    var parts = hostCand.Candidate.Split(' ');
                    if (parts.Length >= 6 && int.TryParse(parts[5], out int actualPort))
                    {
                        serverCandidates.Insert(0, new WebRtcIceCandidateDto
                        {
                            SessionId = sessionId,
                            Side = "answerer",
                            Candidate = $"1 1 udp 2130706431 {_options.AdvertisedIp} {actualPort} typ host generation 0",
                            SdpMid = "0",
                            SdpMLineIndex = 0
                        });
                    }
                }
            }

            _sessions[sessionId] = new BrowserSession(sessionId, pc, serverCandidates, DateTime.UtcNow);

            _logger.LogInformation("[WebRTC-Browser] Peer session {SessionId} created", sessionId);

            return new WebRtcAnswer
            {
                SessionId = sessionId,
                Sdp = answer.sdp,
                GatheredCandidates = serverCandidates.ToList()
            };
        }

        public void AddBrowserIceCandidate(string sessionId, WebRtcIceCandidateDto candidate)
        {
            if (_sessions.TryGetValue(sessionId, out BrowserSession? session))
            {
                session.PeerConnection.addIceCandidate(new RTCIceCandidateInit
                {
                    candidate = candidate.Candidate,
                    sdpMid = candidate.SdpMid,
                    sdpMLineIndex = candidate.SdpMLineIndex.HasValue ? (ushort)candidate.SdpMLineIndex.Value : (ushort)0
                });
            }
        }

        public List<WebRtcIceCandidateDto> GetServerCandidates(string sessionId)
        {
            return _sessions.TryGetValue(sessionId, out BrowserSession? session)
                ? session.ServerCandidates.ToList()
                : [];
        }

        // 180 KB raw per fragment — base64 ≈ 240 KB, safely under the 262144-byte DataChannel limit.
        private const int MaxBlobFragmentBytes = 180 * 1024;

        private async Task HandleDataChannelMessageAsync(string sessionId, RTCDataChannel channel, byte[] data)
        {
            if (data.Length < 2 || (ProtocolKind)data[0] != ProtocolKind.Content)
                return;

            ContentMessage? message;
            try
            {
                message = ContentMessage.Deserialize(new ReadOnlyMemory<byte>(data, 1, data.Length - 1));
            }
            catch
            {
                return;
            }

            if (message == null)
                return;

            if (message is GetBlob getBlob)
            {
                await HandleGetBlobAsync(sessionId, channel, getBlob);
                return;
            }

            ContentMessage? response = await DispatchContentMessageAsync(message);

            if (response != null)
            {
                var responseJson = JsonSerializer.Serialize<ContentMessage>(response);
                var responseJsonBytes = Encoding.UTF8.GetBytes(responseJson);
                var payload = new byte[1 + responseJsonBytes.Length];
                payload[0] = (byte)ProtocolKind.Content;
                Array.Copy(responseJsonBytes, 0, payload, 1, responseJsonBytes.Length);

                try { channel.send(payload); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[WebRTC-Browser] Peer session {SessionId} failed to send response", sessionId);
                }
            }
        }

        private async Task HandleGetBlobAsync(string sessionId, RTCDataChannel channel, GetBlob request)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var blobStore = scope.ServiceProvider.GetRequiredService<IBlobStore>();
            var hash = new BlobHash(request.BlobHash);

            if (!blobStore.Exists(hash)) return;

            using Stream? stream = await blobStore.OpenReadAsync(hash);
            if (stream == null) return;

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var blobData = ms.ToArray();

            int totalChunks = Math.Max(1, (int)Math.Ceiling((double)blobData.Length / MaxBlobFragmentBytes));

            for (int i = 0; i < totalChunks; i++)
            {
                int offset = i * MaxBlobFragmentBytes;
                int length = Math.Min(MaxBlobFragmentBytes, blobData.Length - offset);
                var fragment = new byte[length];
                Array.Copy(blobData, offset, fragment, 0, length);

                ContentMessage msg = totalChunks == 1
                    ? new BlobData { BlobHash = request.BlobHash, Data = fragment, RequestId = request.RequestId, SenderPort = request.SenderPort }
                    : new BlobDataChunk { BlobHash = request.BlobHash, Data = fragment, ChunkIndex = i, TotalChunks = totalChunks, RequestId = request.RequestId, SenderPort = request.SenderPort };

                var msgJson = JsonSerializer.Serialize<ContentMessage>(msg);
                var msgBytes = Encoding.UTF8.GetBytes(msgJson);
                var payload = new byte[1 + msgBytes.Length];
                payload[0] = (byte)ProtocolKind.Content;
                Array.Copy(msgBytes, 0, payload, 1, msgBytes.Length);

                try { channel.send(payload); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[WebRTC-Browser] Peer session {SessionId} failed to send blob fragment {Index}/{Total} for {Hash}",
                        sessionId, i + 1, totalChunks, request.BlobHash);
                    return;
                }
            }
        }

        private async Task<ContentMessage?> DispatchContentMessageAsync(ContentMessage message)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();

            switch (message)
            {
                case GetManifest getManifest:
                {
                    var manifestStore = scope.ServiceProvider.GetRequiredService<IManifestStore>();
                    var manifest = await manifestStore.GetAsync(new ManifestHash(getManifest.ContentHash));

                    if (manifest == null)
                        return null;

                    return new ManifestData
                    {
                        ContentHash = getManifest.ContentHash,
                        Data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest)),
                        RequestId = getManifest.RequestId
                    };
                }

                default:
                    return null;
            }
        }
    }
}
