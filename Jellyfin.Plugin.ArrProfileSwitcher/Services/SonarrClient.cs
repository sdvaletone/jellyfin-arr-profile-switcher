using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Services;

/// <summary>
/// Thin Sonarr v3 REST client (series lookup by <c>tvdbId</c>; quality profile names are
/// whatever the admin has configured in Sonarr — e.g. <c>TV-1080p</c>/<c>TV-4K</c> if
/// following Recyclarr/TRaSH-guide naming conventions).
/// </summary>
public class SonarrClient : ArrClientBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SonarrClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public SonarrClient(HttpClient httpClient, ILogger<SonarrClient> logger)
        : base(httpClient, logger)
    {
    }

    /// <inheritdoc />
    protected override string BaseUrl => Plugin.Instance?.Configuration.SonarrUrl ?? string.Empty;

    /// <inheritdoc />
    protected override string ApiKeyEnvVar => "SONARR_API_KEY";

    /// <summary>
    /// Finds the Sonarr series record for a TVDb id.
    /// </summary>
    /// <param name="tvdbId">The TVDb id (from the Jellyfin item's provider ids).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The series record, or <c>null</c> if not tracked by Sonarr.</returns>
    public Task<JsonObject?> FindSeriesByTvdbIdAsync(int tvdbId, CancellationToken cancellationToken) =>
        FindSingleByQueryAsync($"/api/v3/series?tvdbId={tvdbId}", cancellationToken);

    /// <summary>
    /// Applies a new quality profile to a series.
    /// </summary>
    /// <param name="series">The full series record from <see cref="FindSeriesByTvdbIdAsync"/>.</param>
    /// <param name="qualityProfileId">The target quality profile id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> on success.</returns>
    public Task<bool> SetQualityProfileAsync(JsonObject series, int qualityProfileId, CancellationToken cancellationToken)
    {
        if (series["id"]?.GetValue<int>() is not int seriesId)
        {
            Logger.LogWarning("Sonarr series record had no usable 'id' field; cannot update quality profile.");
            return Task.FromResult(false);
        }

        return UpdateQualityProfileAsync($"/api/v3/series/{seriesId}", series, qualityProfileId, cancellationToken);
    }

    /// <summary>
    /// Triggers a whole-series search (e.g. after a profile upgrade).
    /// </summary>
    /// <param name="seriesId">The Sonarr series id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> on success.</returns>
    public Task<bool> TriggerSearchAsync(int seriesId, CancellationToken cancellationToken) =>
        TriggerCommandAsync(new { name = "SeriesSearch", seriesId }, cancellationToken);
}
