using System;
using System.Collections.Concurrent;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Services;

/// <summary>
/// In-memory per-item cooldown so a double-click or accidental repeat request can't
/// fire the same Radarr/Sonarr profile-change + search twice in a row.
/// </summary>
/// <remarks>
/// Registered as a DI singleton (see <see cref="PluginServiceRegistrator"/>) so state
/// is shared across requests, same pattern as
/// <c>src/jellyfin-loudness-normalizer/.../Services/LoudnessNormalizerState.cs</c>.
/// Lost on Jellyfin restart, which is fine — the cooldown only needs to survive a
/// handful of minutes, not across restarts.
/// </remarks>
public class ArrUpgradeThrottleState
{
    private readonly ConcurrentDictionary<Guid, DateTime> _lastRequestUtc = new();

    /// <summary>
    /// Attempts to reserve an upgrade slot for an item.
    /// </summary>
    /// <param name="itemId">The Jellyfin item id.</param>
    /// <param name="window">The cooldown window.</param>
    /// <returns><c>true</c> if no request for this item was reserved within <paramref name="window"/>.</returns>
    public bool TryReserve(Guid itemId, TimeSpan window)
    {
        var now = DateTime.UtcNow;

        if (_lastRequestUtc.TryGetValue(itemId, out var last) && now - last < window)
        {
            return false;
        }

        _lastRequestUtc[itemId] = now;
        return true;
    }

    /// <summary>
    /// Returns how much of the cooldown window remains for an item, or
    /// <see cref="TimeSpan.Zero"/> if none is active.
    /// </summary>
    /// <param name="itemId">The Jellyfin item id.</param>
    /// <param name="window">The cooldown window.</param>
    /// <returns>The remaining cooldown, or <see cref="TimeSpan.Zero"/>.</returns>
    public TimeSpan GetRemainingCooldown(Guid itemId, TimeSpan window)
    {
        if (!_lastRequestUtc.TryGetValue(itemId, out var last))
        {
            return TimeSpan.Zero;
        }

        var elapsed = DateTime.UtcNow - last;
        return elapsed >= window ? TimeSpan.Zero : window - elapsed;
    }
}
