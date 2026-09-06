using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Configuration;

/// <summary>
/// Plugin configuration, XML-serialized by Jellyfin into
/// <c>/config/data/plugins/configurations/&lt;assembly-name&gt;.xml</c>.
/// </summary>
/// <remarks>
/// Deliberately holds no secrets. Radarr/Sonarr API keys come only from the
/// <c>RADARR_API_KEY</c> / <c>SONARR_API_KEY</c> environment variables on the Jellyfin
/// container (see Services/RadarrClient.cs / SonarrClient.cs) — those are never read
/// from, or written back to, this config or its admin page.
/// </remarks>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the Radarr base URL. Defaults to the container DNS name on the
    /// shared `media_stack` Docker network — traffic never leaves the host.
    /// </summary>
    public string RadarrUrl { get; set; } = "http://radarr:7878";

    /// <summary>
    /// Gets or sets the Sonarr base URL. Same network-locality rationale as <see cref="RadarrUrl"/>.
    /// </summary>
    public string SonarrUrl { get; set; } = "http://sonarr:8989";

    /// <summary>
    /// Gets or sets the cooldown, in minutes, before the same item can be switched
    /// again. Prevents double-click / accidental repeat search-command floods.
    /// </summary>
    public int ThrottleMinutes { get; set; } = 10;

    /// <summary>
    /// Gets or sets the admin-curated, user-selectable profile options for Radarr
    /// (movies).
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays — XmlSerializer requires a settable array here.
    public ProfileOptionMapping[] RadarrProfileOptions { get; set; } = Array.Empty<ProfileOptionMapping>();

    /// <summary>
    /// Gets or sets the admin-curated, user-selectable profile options for Sonarr
    /// (series).
    /// </summary>
    public ProfileOptionMapping[] SonarrProfileOptions { get; set; } = Array.Empty<ProfileOptionMapping>();
#pragma warning restore CA1819
}
