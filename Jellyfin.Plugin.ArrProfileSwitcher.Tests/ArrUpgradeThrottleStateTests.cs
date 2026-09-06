using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.ArrProfileSwitcher.Services;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Tests;

public class ArrUpgradeThrottleStateTests
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    [Fact]
    public void TryReserve_FirstCallForItem_Succeeds()
    {
        var state = new ArrUpgradeThrottleState();

        Assert.True(state.TryReserve(Guid.NewGuid(), Window));
    }

    [Fact]
    public void TryReserve_SecondCallWithinWindow_Fails()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var state = new ArrUpgradeThrottleState(() => now);
        var itemId = Guid.NewGuid();

        Assert.True(state.TryReserve(itemId, Window));
        Assert.False(state.TryReserve(itemId, Window));
    }

    [Fact]
    public void TryReserve_AfterWindowElapses_SucceedsAgain()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var state = new ArrUpgradeThrottleState(() => now);
        var itemId = Guid.NewGuid();

        Assert.True(state.TryReserve(itemId, Window));

        now = now.Add(Window).AddSeconds(1);

        Assert.True(state.TryReserve(itemId, Window));
    }

    [Fact]
    public void TryReserve_DifferentItems_DoNotShareCooldown()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var state = new ArrUpgradeThrottleState(() => now);

        Assert.True(state.TryReserve(Guid.NewGuid(), Window));
        Assert.True(state.TryReserve(Guid.NewGuid(), Window));
    }

    [Fact]
    public async Task TryReserve_ConcurrentCallsForSameItem_ExactlyOneWins()
    {
        // Regression test for a prior TOCTOU race: a separate TryGetValue + indexer-set
        // let two concurrent requests both observe "no active cooldown" and both win.
        // With the fix (ConcurrentDictionary.AddOrUpdate), exactly one caller out of many
        // concurrent attempts on the same item must succeed.
        var state = new ArrUpgradeThrottleState();
        var itemId = Guid.NewGuid();

        var tasks = new Task<bool>[50];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => state.TryReserve(itemId, Window));
        }

        var results = await Task.WhenAll(tasks);

        Assert.Single(Array.FindAll(results, r => r));
    }

    [Fact]
    public void GetRemainingCooldown_UnreservedItem_ReturnsZero()
    {
        var state = new ArrUpgradeThrottleState();

        Assert.Equal(TimeSpan.Zero, state.GetRemainingCooldown(Guid.NewGuid(), Window));
    }

    [Fact]
    public void GetRemainingCooldown_ReservedItem_ReturnsPositiveRemaining()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var state = new ArrUpgradeThrottleState(() => now);
        var itemId = Guid.NewGuid();

        state.TryReserve(itemId, Window);
        now = now.AddMinutes(4);

        var remaining = state.GetRemainingCooldown(itemId, Window);

        Assert.True(remaining > TimeSpan.Zero && remaining <= TimeSpan.FromMinutes(6));
    }

    [Fact]
    public void GetRemainingCooldown_AfterWindowElapses_ReturnsZero()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var state = new ArrUpgradeThrottleState(() => now);
        var itemId = Guid.NewGuid();

        state.TryReserve(itemId, Window);
        now = now.Add(Window).AddSeconds(1);

        Assert.Equal(TimeSpan.Zero, state.GetRemainingCooldown(itemId, Window));
    }
}
