using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Replication;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace MangaMesh.Peer.Tests.Core.Replication;

public class TieredReplicationPolicyTests
{
    private static TieredReplicationPolicy BuildPolicy(bool isSuperSeeder = false)
    {
        var opts = new ReplicationOptions
        {
            IsSuperSeeder = isSuperSeeder,
            NewReleaseTargetReplicas = 25,
            ActiveTargetReplicas = 12,
            OlderTargetReplicas = 5,
            ArchivalMinimumReplicas = 3,
            AbsoluteMinimumReplicas = 3,
            NewReleaseAgeDays = 7,
            ActiveAgeDays = 90
        };
        return new TieredReplicationPolicy(Options.Create(opts));
    }

    private static ChapterManifest MakeManifest(int ageDays) => new()
    {
        ChapterId = "ch-1",
        SeriesId = "series-1",
        Title = "Test",
        Language = "en",
        ScanGroup = "group",
        CreatedUtc = DateTime.UtcNow.AddDays(-ageDays)
    };

    // ── GetTargetForAge ────────────────────────────────────────────────────────

    [Fact]
    public void GetTargetForAge_NewRelease_ReturnsNewReleaseTarget()
    {
        var policy = BuildPolicy();
        var target = policy.GetTargetForAge(TimeSpan.FromDays(3));
        Assert.Equal(25, target.TargetReplicas);
    }

    [Fact]
    public void GetTargetForAge_ActiveAge_ReturnsActiveTarget()
    {
        var policy = BuildPolicy();
        var target = policy.GetTargetForAge(TimeSpan.FromDays(30));
        Assert.Equal(12, target.TargetReplicas);
    }

    [Fact]
    public void GetTargetForAge_OlderAge_ReturnsOlderTarget()
    {
        // Between 90 and 270 days (ActiveAgeDays * 3)
        var policy = BuildPolicy();
        var target = policy.GetTargetForAge(TimeSpan.FromDays(180));
        Assert.Equal(5, target.TargetReplicas);
    }

    [Fact]
    public void GetTargetForAge_ArchivalAge_ReturnsArchivalTarget()
    {
        // More than 270 days
        var policy = BuildPolicy();
        var target = policy.GetTargetForAge(TimeSpan.FromDays(400));
        Assert.Equal(3, target.TargetReplicas);
    }

    [Fact]
    public void GetTargetForAge_ExactlyAtNewReleaseBoundary_ReturnsNewReleaseTarget()
    {
        var policy = BuildPolicy();
        var target = policy.GetTargetForAge(TimeSpan.FromDays(7));
        Assert.Equal(25, target.TargetReplicas);
    }

    [Fact]
    public void GetTargetForAge_ExactlyAtActiveBoundary_ReturnsActiveTarget()
    {
        var policy = BuildPolicy();
        var target = policy.GetTargetForAge(TimeSpan.FromDays(90));
        Assert.Equal(12, target.TargetReplicas);
    }

    // ── Super-seeder override ──────────────────────────────────────────────────

    [Fact]
    public void GetTargetForAge_SuperSeeder_AlwaysReturnsNewReleaseTarget()
    {
        var policy = BuildPolicy(isSuperSeeder: true);

        // All tiers should return new-release target for super-seeder
        Assert.Equal(25, policy.GetTargetForAge(TimeSpan.FromDays(3)).TargetReplicas);
        Assert.Equal(25, policy.GetTargetForAge(TimeSpan.FromDays(30)).TargetReplicas);
        Assert.Equal(25, policy.GetTargetForAge(TimeSpan.FromDays(180)).TargetReplicas);
        Assert.Equal(25, policy.GetTargetForAge(TimeSpan.FromDays(400)).TargetReplicas);
    }

    // ── GetTarget delegates to GetTargetForAge ─────────────────────────────────

    [Fact]
    public void GetTarget_NewManifest_ReturnsNewReleaseTarget()
    {
        var policy = BuildPolicy();
        var manifest = MakeManifest(ageDays: 1);
        var target = policy.GetTarget(manifest);
        Assert.Equal(25, target.TargetReplicas);
    }

    [Fact]
    public void GetTarget_OldManifest_ReturnsArchivalTarget()
    {
        var policy = BuildPolicy();
        var manifest = MakeManifest(ageDays: 500);
        var target = policy.GetTarget(manifest);
        Assert.Equal(3, target.TargetReplicas);
    }

    // ── MinimumReplicas is always set ──────────────────────────────────────────

    [Fact]
    public void GetTargetForAge_MinimumReplicas_IsNeverZero()
    {
        var policy = BuildPolicy();
        foreach (int days in new[] { 1, 30, 180, 400 })
        {
            var target = policy.GetTargetForAge(TimeSpan.FromDays(days));
            Assert.True(target.MinimumReplicas > 0, $"MinimumReplicas should be > 0 for age {days} days");
        }
    }
}
