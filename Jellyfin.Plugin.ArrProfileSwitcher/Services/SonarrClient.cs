using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Services;

/// <summary>
/// Thin Sonarr v3 REST client. Endpoint shapes confirmed live against this repo's own
/// NUC Sonarr instance (series lookup by <c>tvdbId</c>, quality profile names
/// <c>TV-1080p</c>/<c>TV-4K</c>/etc. from Recyclarr's TRaSH sync).
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
    protected override string BaseUrl =>
        Plugin.Instance?.Configuration.SonarrUrl is { Length: > 0 } url ? url : "http://sonarr:8989";

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
        var seriesId = series["id"]!.GetValue<int>();
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
