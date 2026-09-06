namespace Jellyfin.Plugin.ArrProfileSwitcher.Api.Models;

/// <summary>
/// Response for <c>POST /ArrProfileSwitcher/Upgrade</c>.
/// </summary>
public class UpgradeResultDto
{
    /// <summary>
    /// Gets or sets a value indicating whether the profile change (and, when
    /// applicable, the search trigger) succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets a short, user-facing message (e.g. "Already on this profile.",
    /// "Try again in 8 more minutes."). Never includes arr API details.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether a Radarr/Sonarr search was triggered.
    /// </summary>
    public bool SearchTriggered { get; set; }
}
