using System.Collections.Generic;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Api.Models;

/// <summary>
/// Response for <c>GET /ArrProfileSwitcher/Status?itemId=</c> - everything the client
/// script needs to decide whether to show the picker and what to show in it.
/// </summary>
public class StatusDto
{
    /// <summary>
    /// Gets or sets a value indicating whether this item is tracked by Radarr or Sonarr
    /// at all. When <c>false</c>, the client injects nothing.
    /// </summary>
    public bool Tracked { get; set; }

    /// <summary>
    /// Gets or sets the item's current Radarr/Sonarr quality profile name, for display only.
    /// </summary>
    public string? CurrentProfileName { get; set; }

    /// <summary>
    /// Gets the admin-curated options selectable for this item, in configured order.
    /// Empty when the admin hasn't configured any options for this instance yet.
    /// </summary>
    public IList<ProfileOptionDto> Options { get; } = new List<ProfileOptionDto>();

    /// <summary>
    /// Gets or sets an optional human-readable reason when <see cref="Tracked"/> is
    /// <c>false</c> (e.g. "Not found in Radarr."). Never includes arr API details.
    /// </summary>
    public string? Message { get; set; }
}
