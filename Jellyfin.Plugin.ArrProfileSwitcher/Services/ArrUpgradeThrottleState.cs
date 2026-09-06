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
    private readonly Func<DateTime> _utcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrUpgradeThrottleState"/> class.
    /// </summary>
    /// <param name="utcNow">
    /// Clock used for cooldown timestamps. Defaults to <see cref="DateTime.UtcNow"/>;
    /// overridable only so tests can exercise the cooldown window deterministically
    /// without real sleeps.
    /// </param>
    public ArrUpgradeThrottleState(Func<DateTime>? utcNow = null)
    {
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Attempts to reserve an upgrade slot for an item.
    /// </summary>
    /// <param name="itemId">The Jellyfin item id.</param>
    /// <param name="window">The cooldown window.</param>
    /// <returns><c>true</c> if no request for this item was reserved within <paramref name="window"/>.</returns>
    public bool TryReserve(Guid itemId, TimeSpan window)
    {
        var now = _utcNow();
        var reserved = false;

        // AddOrUpdate's factories can run more than once under contention, but the
        // dictionary guarantees only one call's return value is ever stored per key —
        // so exactly one concurrent caller observes reserved=true even when several
        // race here for the same itemId (fixes a prior TOCTOU: separate
        // TryGetValue + indexer-set let two concurrent requests both pass the check).
        _lastRequestUtc.AddOrUpdate(
            itemId,
            _ =>
            {
                reserved = true;
                return now;
            },
            (_, last) =>
            {
                if (now - last < window)
                {
                    return last;
                }

                reserved = true;
                return now;
            });

        return reserved;
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

        var elapsed = _utcNow() - last;
        return elapsed >= window ? TimeSpan.Zero : window - elapsed;
    }
}
