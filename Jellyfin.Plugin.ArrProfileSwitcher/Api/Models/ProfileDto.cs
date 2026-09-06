namespace Jellyfin.Plugin.ArrProfileSwitcher.Api.Models;

/// <summary>
/// A real Radarr/Sonarr quality profile, as returned to the admin config page only.
/// </summary>
public class ProfileDto
{
    /// <summary>
    /// Gets or sets the Radarr/Sonarr quality profile id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the Radarr/Sonarr quality profile name (e.g. "Movies-4K").
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
