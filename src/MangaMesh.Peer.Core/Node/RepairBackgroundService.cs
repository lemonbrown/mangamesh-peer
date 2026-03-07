using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Replication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Core.Node;

/// <summary>
/// Periodically invokes <see cref="IRepairScheduler.ScanAndRepairAsync"/> to find
/// under-replicated chunks and push them to their ring-assigned peers.
/// </summary>
public sealed class RepairBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReplicationOptions _opts;
    private readonly ILogger<RepairBackgroundService> _logger;

    public RepairBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReplicationOptions> options,
        ILogger<RepairBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _opts = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.Enabled)
            return;

        // Give the node time to bootstrap before first repair pass
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        _logger.LogInformation("Repair background service started (interval={Interval}s)", _opts.RepairScanIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IRepairScheduler scheduler = scope.ServiceProvider.GetRequiredService<IRepairScheduler>();

                _logger.LogDebug("Starting repair scan");
                //await scheduler.ScanAndRepairAsync(stoppingToken);
                _logger.LogDebug("Repair scan complete");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Repair scan failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_opts.RepairScanIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
