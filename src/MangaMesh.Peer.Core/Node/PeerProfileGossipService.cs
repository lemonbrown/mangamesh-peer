using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Replication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Core.Node;

/// <summary>
/// Periodically broadcasts the local peer's storage profile to a sample of routing
/// table neighbours via <see cref="ChapterHealthGossip"/>.
/// Storage profile fields are also piggy-backed onto every outgoing <see cref="Transport.DhtMessage"/>
/// via <see cref="DhtNode"/> — this service handles the active gossip push.
/// </summary>
public sealed class PeerProfileGossipService : BackgroundService
{
    private readonly IDhtNode _dhtNode;
    private readonly IPeerStorageProfileProvider _profileProvider;
    private readonly IChapterHealthMonitor _healthMonitor;
    private readonly ReplicationOptions _opts;
    private readonly ILogger<PeerProfileGossipService> _logger;

    public PeerProfileGossipService(
        IDhtNode dhtNode,
        IPeerStorageProfileProvider profileProvider,
        IChapterHealthMonitor healthMonitor,
        IOptions<ReplicationOptions> options,
        ILogger<PeerProfileGossipService> logger)
    {
        _dhtNode = dhtNode;
        _profileProvider = profileProvider;
        _healthMonitor = healthMonitor;
        _opts = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.Enabled)
            return;

        _logger.LogInformation("Peer profile gossip service started (interval={Interval}s)", _opts.GossipIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_opts.GossipIntervalSeconds), stoppingToken);
                await GossipAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gossip cycle failed");
            }
        }
    }

    private async Task GossipAsync(CancellationToken ct)
    {
        PeerStorageProfile profile = _profileProvider.GetLocalProfile();
        IReadOnlyList<ChapterHealthState> healthStates = _healthMonitor.GetAllHealthStates();

        if (healthStates.Count == 0)
            return;

        // Sample up to 5 random neighbours from routing table
        var neighbours = _dhtNode.RoutingTable.GetAll()
            .OrderBy(_ => Guid.NewGuid())
            .Take(5)
            .ToList();

        if (neighbours.Count == 0)
            return;

        var gossipMsg = new ChapterHealthGossip
        {
            Items = healthStates
                .OrderByDescending(s => s.RareChunkCount)
                .Take(20)
                .ToList()
        };

        _logger.LogDebug("Gossiping {Count} chapter health states to {Peers} peers",
            gossipMsg.Items.Count, neighbours.Count);

        foreach (var neighbour in neighbours)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _dhtNode.SendContentRequestAsync(neighbour.Address, gossipMsg, TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Gossip to {Host} failed", neighbour.Address.Host);
            }
        }
    }
}
