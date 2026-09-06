using System.Collections.Generic;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Api.Models;

/// <summary>
/// Response for <c>GET /ArrProfileSwitcher/Profiles</c> (admin-only) — the real
/// quality profiles on each instance, used to populate the option-mapping editor.
/// </summary>
public class ProfilesResponseDto
{
    /// <summary>
    /// Gets the Radarr quality profiles.
    /// </summary>
    public IList<ProfileDto> Radarr { get; } = new List<ProfileDto>();

    /// <summary>
    /// Gets the Sonarr quality profiles.
    /// </summary>
    public IList<ProfileDto> Sonarr { get; } = new List<ProfileDto>();
}
