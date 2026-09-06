using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Services;

/// <summary>
/// Thin Radarr v3 REST client (movie lookup by <c>tmdbId</c>; quality profile names are
/// whatever the admin has configured in Radarr — e.g. <c>Movies-1080p</c>/<c>Movies-4K</c>
/// if following Recyclarr/TRaSH-guide naming conventions).
/// </summary>
public class RadarrClient : ArrClientBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RadarrClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public RadarrClient(HttpClient httpClient, ILogger<RadarrClient> logger)
        : base(httpClient, logger)
    {
    }

    /// <inheritdoc />
    protected override string BaseUrl => Plugin.Instance?.Configuration.RadarrUrl ?? string.Empty;

    /// <inheritdoc />
    protected override string ApiKeyEnvVar => "RADARR_API_KEY";

    /// <summary>
    /// Finds the Radarr movie record for a TMDb id.
    /// </summary>
    /// <param name="tmdbId">The TMDb id (from the Jellyfin item's provider ids).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The movie record, or <c>null</c> if not tracked by Radarr.</returns>
    public Task<JsonObject?> FindMovieByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken) =>
        FindSingleByQueryAsync($"/api/v3/movie?tmdbId={tmdbId}", cancellationToken);

    /// <summary>
    /// Applies a new quality profile to a movie.
    /// </summary>
    /// <param name="movie">The full movie record from <see cref="FindMovieByTmdbIdAsync"/>.</param>
    /// <param name="qualityProfileId">The target quality profile id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> on success.</returns>
    public Task<bool> SetQualityProfileAsync(JsonObject movie, int qualityProfileId, CancellationToken cancellationToken)
    {
        if (movie["id"]?.GetValue<int>() is not int movieId)
        {
            Logger.LogWarning("Radarr movie record had no usable 'id' field; cannot update quality profile.");
            return Task.FromResult(false);
        }

        return UpdateQualityProfileAsync($"/api/v3/movie/{movieId}", movie, qualityProfileId, cancellationToken);
    }

    /// <summary>
    /// Triggers a search for a movie (e.g. after a profile upgrade).
    /// </summary>
    /// <param name="movieId">The Radarr movie id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> on success.</returns>
    public Task<bool> TriggerSearchAsync(int movieId, CancellationToken cancellationToken) =>
        TriggerCommandAsync(new { name = "MoviesSearch", movieIds = new[] { movieId } }, cancellationToken);
}
