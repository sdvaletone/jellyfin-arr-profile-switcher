using System;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Configuration;

/// <summary>
/// One admin-curated, user-selectable quality-profile option for a single Radarr or
/// Sonarr instance.
/// </summary>
/// <remarks>
/// <see cref="OptionId"/> is the plugin's own opaque key - it is what the client ever
/// sends back in <c>POST /ArrProfileSwitcher/Upgrade</c>. The real Radarr/Sonarr
/// <see cref="ArrProfileId"/> is resolved server-side from this mapping and never
/// accepted directly from a client, so a caller can only ever pick one of the options
/// an admin explicitly enabled here.
/// </remarks>
public class ProfileOptionMapping
{
    /// <summary>
    /// Gets or sets the opaque id the client refers to this option by.
    /// </summary>
    public Guid OptionId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the admin-editable label shown to users (e.g. "4K").
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the real Radarr/Sonarr quality profile id this option applies.
    /// </summary>
    public int ArrProfileId { get; set; }

    /// <summary>
    /// Gets or sets the Radarr/Sonarr quality profile's own name, kept only so the
    /// admin config page can show it next to the label without a live re-fetch.
    /// </summary>
    public string ArrProfileName { get; set; } = string.Empty;
}
