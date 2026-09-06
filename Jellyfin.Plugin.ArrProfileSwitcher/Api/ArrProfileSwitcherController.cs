using System;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ArrProfileSwitcher.Api.Models;
using Jellyfin.Plugin.ArrProfileSwitcher.Configuration;
using Jellyfin.Plugin.ArrProfileSwitcher.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Api;

/// <summary>
/// Quality-profile status and switch API consumed by the injected client script
/// (<c>ClientScript/arr-profile-switcher.js</c>) and the admin config page.
/// </summary>
/// <remarks>
/// Security model (see plugin README): Radarr/Sonarr API keys never leave the server —
/// <see cref="GetProfiles"/> is admin-only and only ever returns profile names/ids for
/// the mapping editor; <see cref="GetStatus"/> and <see cref="PostUpgrade"/> are open to
/// any authenticated Jellyfin user but only ever expose/accept the plugin's own opaque
/// <see cref="ProfileOptionMapping.OptionId"/> — never a raw Radarr/Sonarr profile id.
/// </remarks>
[ApiController]
[Route("ArrProfileSwitcher")]
[Produces(MediaTypeNames.Application.Json)]
public class ArrProfileSwitcherController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly RadarrClient _radarr;
    private readonly SonarrClient _sonarr;
    private readonly ArrUpgradeThrottleState _throttle;
    private readonly ILogger<ArrProfileSwitcherController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrProfileSwitcherController"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="radarr">The Radarr client.</param>
    /// <param name="sonarr">The Sonarr client.</param>
    /// <param name="throttle">The shared upgrade-cooldown state.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public ArrProfileSwitcherController(
        ILibraryManager libraryManager,
        RadarrClient radarr,
        SonarrClient sonarr,
        ArrUpgradeThrottleState throttle,
        ILogger<ArrProfileSwitcherController> logger)
    {
        _libraryManager = libraryManager;
        _radarr = radarr;
        _sonarr = sonarr;
        _throttle = throttle;
        _logger = logger;
    }

    /// <summary>
    /// Lists each instance's real quality profiles, for the admin config page's
    /// option-mapping editor.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Profiles returned (possibly empty for an instance that's unreachable).</response>
    /// <returns>The profiles.</returns>
    [HttpGet("Profiles")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfilesResponseDto>> GetProfiles(CancellationToken cancellationToken)
    {
        var response = new ProfilesResponseDto();

        foreach (var profile in await _radarr.GetQualityProfilesAsync(cancellationToken).ConfigureAwait(false))
        {
            response.Radarr.Add(profile);
        }

        foreach (var profile in await _sonarr.GetQualityProfilesAsync(cancellationToken).ConfigureAwait(false))
        {
            response.Sonarr.Add(profile);
        }

        return Ok(response);
    }

    /// <summary>
    /// Returns whether an item is tracked by Radarr/Sonarr and the options a user may
    /// switch it to.
    /// </summary>
    /// <param name="itemId">The Jellyfin item id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Status returned (untracked items still return 200 with <c>Tracked: false</c>).</response>
    /// <returns>The status.</returns>
    [HttpGet("Status")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<StatusDto>> GetStatus([FromQuery] Guid itemId, CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return Ok(new StatusDto { Tracked = false, Message = "Item not found." });
        }

        item = ResolveEffectiveItem(item);

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        if (item is Movie movie)
        {
            if (!TryGetProviderId(movie.ProviderIds, "Tmdb", out var tmdbId))
            {
                return Ok(new StatusDto { Tracked = false, Message = "No TMDb id on this item." });
            }

            var record = await _radarr.FindMovieByTmdbIdAsync(tmdbId, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                return Ok(new StatusDto { Tracked = false, Message = "Not found in Radarr." });
            }

            var currentProfileId = record["qualityProfileId"]?.GetValue<int>() ?? -1;
            return Ok(BuildStatus(config.RadarrProfileOptions, currentProfileId));
        }

        if (item is Series series)
        {
            if (!TryGetProviderId(series.ProviderIds, "Tvdb", out var tvdbId))
            {
                return Ok(new StatusDto { Tracked = false, Message = "No TVDb id on this item." });
            }

            var record = await _sonarr.FindSeriesByTvdbIdAsync(tvdbId, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                return Ok(new StatusDto { Tracked = false, Message = "Not found in Sonarr." });
            }

            var currentProfileId = record["qualityProfileId"]?.GetValue<int>() ?? -1;
            return Ok(BuildStatus(config.SonarrProfileOptions, currentProfileId));
        }

        return Ok(new StatusDto { Tracked = false, Message = "Unsupported item type." });
    }

    /// <summary>
    /// Switches an item to an admin-curated quality-profile option and triggers a search.
    /// </summary>
    /// <param name="request">The item and chosen option.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Handled (check <see cref="UpgradeResultDto.Success"/> for outcome).</response>
    /// <response code="400">Malformed request.</response>
    /// <returns>The result.</returns>
    [HttpPost("Upgrade")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UpgradeResultDto>> PostUpgrade([FromBody] UpgradeRequestDto request, CancellationToken cancellationToken)
    {
        if (request is null || request.ItemId == Guid.Empty || request.OptionId == Guid.Empty)
        {
            return BadRequest("ItemId and OptionId are required.");
        }

        var item = _libraryManager.GetItemById(request.ItemId);
        if (item is null)
        {
            return Ok(new UpgradeResultDto { Success = false, Message = "Item not found." });
        }

        item = ResolveEffectiveItem(item);

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var requestingUser = HttpContext.User?.Identity?.Name ?? "unknown";

        if (item is Movie movie)
        {
            var option = config.RadarrProfileOptions.FirstOrDefault(o => o.OptionId == request.OptionId);
            if (option is null)
            {
                return Ok(new UpgradeResultDto { Success = false, Message = "That option is not configured." });
            }

            if (!TryGetProviderId(movie.ProviderIds, "Tmdb", out var tmdbId))
            {
                return Ok(new UpgradeResultDto { Success = false, Message = "No TMDb id on this item." });
            }

            var record = await _radarr.FindMovieByTmdbIdAsync(tmdbId, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                return Ok(new UpgradeResultDto { Success = false, Message = "Not found in Radarr." });
            }

            return await ApplyAsync(
                request.ItemId,
                item.Name,
                record["qualityProfileId"]?.GetValue<int>() ?? -1,
                option,
                async () => await _radarr.SetQualityProfileAsync(record, option.ArrProfileId, cancellationToken).ConfigureAwait(false),
                async () => await _radarr.TriggerSearchAsync(record["id"]!.GetValue<int>(), cancellationToken).ConfigureAwait(false),
                requestingUser,
                config.ThrottleMinutes).ConfigureAwait(false);
        }

        if (item is Series series)
        {
            var option = config.SonarrProfileOptions.FirstOrDefault(o => o.OptionId == request.OptionId);
            if (option is null)
            {
                return Ok(new UpgradeResultDto { Success = false, Message = "That option is not configured." });
            }

            if (!TryGetProviderId(series.ProviderIds, "Tvdb", out var tvdbId))
            {
                return Ok(new UpgradeResultDto { Success = false, Message = "No TVDb id on this item." });
            }

            var record = await _sonarr.FindSeriesByTvdbIdAsync(tvdbId, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                return Ok(new UpgradeResultDto { Success = false, Message = "Not found in Sonarr." });
            }

            return await ApplyAsync(
                request.ItemId,
                item.Name,
                record["qualityProfileId"]?.GetValue<int>() ?? -1,
                option,
                async () => await _sonarr.SetQualityProfileAsync(record, option.ArrProfileId, cancellationToken).ConfigureAwait(false),
                async () => await _sonarr.TriggerSearchAsync(record["id"]!.GetValue<int>(), cancellationToken).ConfigureAwait(false),
                requestingUser,
                config.ThrottleMinutes).ConfigureAwait(false);
        }

        return Ok(new UpgradeResultDto { Success = false, Message = "Unsupported item type." });
    }

    /// <summary>
    /// Shared apply path: idempotency check, throttle, the actual arr calls, and the audit log line.
    /// </summary>
    private async Task<ActionResult<UpgradeResultDto>> ApplyAsync(
        Guid itemId,
        string? itemName,
        int currentProfileId,
        ProfileOptionMapping option,
        Func<Task<bool>> setProfile,
        Func<Task<bool>> triggerSearch,
        string requestingUser,
        int throttleMinutes)
    {
        if (currentProfileId == option.ArrProfileId)
        {
            return Ok(new UpgradeResultDto { Success = true, Message = $"Already on {option.Label}.", SearchTriggered = false });
        }

        var window = TimeSpan.FromMinutes(Math.Max(1, throttleMinutes));
        if (!_throttle.TryReserve(itemId, window))
        {
            var remaining = _throttle.GetRemainingCooldown(itemId, window);
            return Ok(new UpgradeResultDto
            {
                Success = false,
                Message = $"Already requested recently — try again in {Math.Ceiling(remaining.TotalMinutes)} more minute(s).",
                SearchTriggered = false
            });
        }

        var setOk = await setProfile().ConfigureAwait(false);
        if (!setOk)
        {
            return Ok(new UpgradeResultDto { Success = false, Message = "Could not update the quality profile.", SearchTriggered = false });
        }

        var searchOk = await triggerSearch().ConfigureAwait(false);

        _logger.LogInformation(
            "{User} switched {ItemName} ({ItemId}) from profile {PreviousProfileId} to {NewProfile} ({NewProfileId}); search triggered: {SearchTriggered}",
            requestingUser,
            itemName ?? "(unknown item)",
            itemId,
            currentProfileId,
            option.Label,
            option.ArrProfileId,
            searchOk);

        return Ok(new UpgradeResultDto
        {
            Success = true,
            Message = searchOk ? $"Switched to {option.Label} — search started." : $"Switched to {option.Label}, but the search could not be started.",
            SearchTriggered = searchOk
        });
    }

    private static StatusDto BuildStatus(ProfileOptionMapping[] configuredOptions, int currentProfileId)
    {
        var status = new StatusDto { Tracked = true };

        foreach (var option in configuredOptions)
        {
            var isCurrent = option.ArrProfileId == currentProfileId;
            if (isCurrent)
            {
                status.CurrentProfileName = option.ArrProfileName;
            }

            status.Options.Add(new ProfileOptionDto
            {
                OptionId = option.OptionId,
                Label = option.Label,
                IsCurrent = isCurrent
            });
        }

        return status;
    }

    /// <summary>
    /// Resolves the item that Radarr/Sonarr tracking actually keys off of. Movies and
    /// Series are returned as-is; an Episode or Season is resolved up to its parent
    /// Series (Sonarr tracks the whole series, not individual episodes/seasons) so the
    /// three-dot menu works from inside a show, not just from the series page itself.
    /// Anything else is returned unchanged and falls through to "Unsupported item type."
    /// </summary>
    private BaseItem ResolveEffectiveItem(BaseItem item)
    {
        Guid? seriesId = item switch
        {
            Episode episode => episode.SeriesId,
            Season season => season.SeriesId,
            _ => null
        };

        if (seriesId is null)
        {
            return item;
        }

        var series = _libraryManager.GetItemById(seriesId.Value);
        return series ?? item;
    }

    private static bool TryGetProviderId(System.Collections.Generic.Dictionary<string, string> providerIds, string key, out int value)
    {
        value = 0;
        return providerIds is not null
            && providerIds.TryGetValue(key, out var raw)
            && int.TryParse(raw, out value);
    }
}
