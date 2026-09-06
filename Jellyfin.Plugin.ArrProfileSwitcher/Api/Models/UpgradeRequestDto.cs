using System;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Api.Models;

/// <summary>
/// Request body for <c>POST /ArrProfileSwitcher/Upgrade</c>.
/// </summary>
/// <remarks>
/// Deliberately carries no arr profile id — only the plugin's own opaque
/// <see cref="OptionId"/>, which the server resolves against the admin-configured
/// mapping for the item's app. See Configuration/ProfileOptionMapping.cs.
/// </remarks>
public class UpgradeRequestDto
{
    /// <summary>
    /// Gets or sets the Jellyfin item id.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the chosen option's opaque id.
    /// </summary>
    public Guid OptionId { get; set; }
}
