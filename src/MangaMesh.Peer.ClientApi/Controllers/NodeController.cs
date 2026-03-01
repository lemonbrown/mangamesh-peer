using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Tracker;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.ClientApi.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class NodeController : ControllerBase
    {
        private readonly INodeIdentityService _nodeIdentity;
        private readonly ISeriesRegistry _trackerClient;
        private readonly IManifestStore _manifestStore;
        private readonly IConfiguration _configuration;
        private readonly IDhtNode _dhtNode;
        private readonly IPeerLocator _peerLocator;

        public NodeController(
            INodeIdentityService nodeIdentity,
            ISeriesRegistry trackerClient,
            IManifestStore manifestStore,
            IConfiguration configuration,
            IDhtNode dhtNode,
            IPeerLocator peerLocator)
        {
            _nodeIdentity = nodeIdentity;
            _trackerClient = trackerClient;
            _manifestStore = manifestStore;
            _configuration = configuration;
            _dhtNode = dhtNode;
            _peerLocator = peerLocator;
        }

        [HttpGet("status")]
        public async Task<IResult> GetStatus()
        {

            var stats = await _trackerClient.GetStatsAsync();
            var (_, seededCount) = await _manifestStore.GetSetHashAsync();
            var trackerUrl = _configuration["TrackerUrl"] ?? "https://localhost:7030";

            return Results.Ok(new
            {
                _nodeIdentity.NodeId,
                _nodeIdentity.IsConnected,
                LastPingUtc = _nodeIdentity.LastPingUtc,
                PeerCount = stats.NodeCount,
                SeededManifests = seededCount,
                TrackerUrl = trackerUrl
            });
        }

        [HttpGet("peers")]
        public IResult GetPeers()
        {
            var entries = _dhtNode.RoutingTable.GetAll();

            var result = entries.Select(e => new
            {
                NodeId = Convert.ToHexString(e.NodeId).ToLowerInvariant(),
                Host = e.Address.Host,
                Port = e.Address.Port,
                HttpApiPort = e.Address.HttpApiPort,
                LastSeenUtc = e.LastSeenUtc
            });

            return Results.Ok(result);
        }

        [HttpGet("peers/manifest/{manifestHash}")]
        public async Task<IResult> GetManifestPeers(string manifestHash)
        {
            try
            {
                var peers = await _peerLocator.GetPeersForManifestAsync(manifestHash);
                var result = peers.Select(p => new
                {
                    nodeId = p.NodeId,
                    lastSeen = p.LastSeen
                });
                return Results.Ok(result);
            }
            catch
            {
                return Results.Ok(Array.Empty<object>());
            }
        }
    }
}
