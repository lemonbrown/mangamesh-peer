using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Replication;
using MangaMesh.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Peer.ClientApi.Controllers;

[ApiController]
[Route("api/replication")]
[Authorize]
public sealed class ReplicationController : ControllerBase
{
    private readonly IChapterHealthMonitor _healthMonitor;
    private readonly IPeerStorageProfileProvider _profileProvider;
    private readonly IReplicationPolicy _policy;
    private readonly IConsistentHashRing _ring;
    private readonly IManifestStore _manifestStore;
    private readonly INodeIdentity _identity;
    private readonly IServiceScopeFactory _scopeFactory;

    public ReplicationController(
        IChapterHealthMonitor healthMonitor,
        IPeerStorageProfileProvider profileProvider,
        IReplicationPolicy policy,
        IConsistentHashRing ring,
        IManifestStore manifestStore,
        INodeIdentity identity,
        IServiceScopeFactory scopeFactory)
    {
        _healthMonitor = healthMonitor;
        _profileProvider = profileProvider;
        _policy = policy;
        _ring = ring;
        _manifestStore = manifestStore;
        _identity = identity;
        _scopeFactory = scopeFactory;
    }

    /// <summary>Returns the local node's current storage profile.</summary>
    [HttpGet("profile")]
    public IActionResult GetProfile()
    {
        return Ok(_profileProvider.GetLocalProfile());
    }

    /// <summary>Returns all known chapter health states.</summary>
    [HttpGet("health")]
    public IActionResult GetAllHealth()
    {
        return Ok(_healthMonitor.GetAllHealthStates());
    }

    /// <summary>Returns health state for a specific chapter manifest.</summary>
    [HttpGet("health/{manifestHash}")]
    public async Task<IActionResult> GetChapterHealth(string manifestHash)
    {
        var manifest = await _manifestStore.GetAsync(new ManifestHash(manifestHash));
        if (manifest is null)
            return NotFound();

        var target = _policy.GetTarget(manifest);

        // Collect all chunk hashes from page manifests that exist locally
        var chunkHashes = manifest.Files
            .Select(f => f.Hash)
            .ToList();

        var state = _healthMonitor.GetHealthState(
            manifest.ChapterId,
            manifestHash,
            chunkHashes,
            target.MinimumReplicas);

        return Ok(new
        {
            State = state,
            Target = target
        });
    }

    /// <summary>Triggers a repair pass for a single chapter.</summary>
    [HttpPost("repair/{manifestHash}")]
    public async Task<IActionResult> RepairChapter(string manifestHash, CancellationToken ct)
    {
        var manifest = await _manifestStore.GetAsync(new ManifestHash(manifestHash));
        if (manifest is null)
            return NotFound();

        using var scope = _scopeFactory.CreateScope();
        var scheduler = scope.ServiceProvider.GetService<IRepairScheduler>();
        if (scheduler is null)
            return StatusCode(503, "Replication service is not enabled");

        await scheduler.RepairChapterAsync(manifestHash, ct);
        return Accepted();
    }

    /// <summary>
    /// Returns the ring-responsible peers for a chunk hash (debug / diagnostics).
    /// </summary>
    [HttpGet("ring/{chunkHash}")]
    public IActionResult GetRingPeers(string chunkHash, [FromQuery] int replicas = 12)
    {
        var peers = _ring.GetResponsiblePeers(chunkHash, replicas);
        bool isLocal = _ring.IsLocallyResponsible(chunkHash, replicas);

        return Ok(new
        {
            ChunkHash = chunkHash,
            TargetReplicas = replicas,
            IsLocallyResponsible = isLocal,
            Peers = peers.Select(p => new
            {
                NodeId = Convert.ToHexString(p.NodeId).ToLowerInvariant(),
                Host = p.Address.Host,
                Port = p.Address.Port,
                FreeStorageMb = p.FreeStorageBytes / (1024.0 * 1024.0),
                UptimeScore = p.UptimeScore
            })
        });
    }
}
