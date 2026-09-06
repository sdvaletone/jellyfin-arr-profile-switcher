using System;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Api.Models;

/// <summary>
/// One selectable option, as exposed to any authenticated user in
/// <c>GET /ArrProfileSwitcher/Status</c>.
/// </summary>
/// <remarks>
/// Deliberately carries only the plugin's own opaque <see cref="OptionId"/> and a
/// display <see cref="Label"/> — never the underlying Radarr/Sonarr profile id, which
/// stays server-side (see Configuration/ProfileOptionMapping.cs).
/// </remarks>
public class ProfileOptionDto
{
    /// <summary>
    /// Gets or sets the opaque option id to send back in <c>POST .../Upgrade</c>.
    /// </summary>
    public Guid OptionId { get; set; }

    /// <summary>
    /// Gets or sets the admin-configured display label (e.g. "4K").
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this option is the item's current profile.
    /// </summary>
    public bool IsCurrent { get; set; }
}
