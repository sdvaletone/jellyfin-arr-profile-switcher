using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ArrProfileSwitcher.Api.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Services;

/// <summary>
/// Shared REST plumbing for talking to a Radarr or Sonarr v3 API instance.
/// </summary>
/// <remarks>
/// Reads a full JSON record, mutates only <c>qualityProfileId</c>, and PUTs the whole
/// object back — Radarr/Sonarr's v3 API requires the complete resource on PUT, so a
/// narrow strongly-typed DTO would silently drop every other field (root folder,
/// tags, images, monitoring…) on save. This is the same read-modify-write pattern
/// commonly used by other *arr integrations for the same reason.
/// <para/>
/// The API key is read fresh from an environment variable on every call rather than
/// cached at startup, and is never logged, returned to a caller, or stored in
/// <see cref="Configuration.PluginConfiguration"/> — see the security notes on
/// <see cref="Configuration.PluginConfiguration"/> and the plugin README.
/// </remarks>
public abstract class ArrClientBase
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrClientBase"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for requests.</param>
    /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
    protected ArrClientBase(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        Logger = logger;
    }

    /// <summary>
    /// Gets the logger for the concrete client.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets the instance's base URL (e.g. <c>http://radarr:7878</c>).
    /// </summary>
    protected abstract string BaseUrl { get; }

    /// <summary>
    /// Gets the name of the environment variable holding this instance's API key.
    /// </summary>
    protected abstract string ApiKeyEnvVar { get; }

    private string? ApiKey => Environment.GetEnvironmentVariable(ApiKeyEnvVar);

    /// <summary>
    /// Gets a value indicating whether both a base URL (admin-configured, no default —
    /// see <see cref="Configuration.PluginConfiguration"/>) and an API key are present.
    /// <see cref="System.Net.Http.HttpClient.SendAsync(System.Net.Http.HttpRequestMessage,CancellationToken)"/>
    /// throws on a relative request URI when <see cref="BaseUrl"/> is empty (no
    /// <c>HttpClient.BaseAddress</c> is set), so every call path must check this first
    /// instead of letting that surface as an unhandled exception.
    /// </summary>
    private bool IsConfigured => !string.IsNullOrEmpty(BaseUrl) && !string.IsNullOrEmpty(ApiKey);

    /// <summary>
    /// Fetches the instance's real quality profiles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The quality profiles, or an empty list if the instance is unreachable or unconfigured.</returns>
    public async Task<IReadOnlyList<ProfileDto>> GetQualityProfilesAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            Logger.LogWarning("Radarr/Sonarr URL or {ApiKeyEnvVar} is not set; cannot reach {BaseUrl}", ApiKeyEnvVar, BaseUrl);
            return Array.Empty<ProfileDto>();
        }

        using var request = NewRequest(HttpMethod.Get, "/api/v3/qualityprofile");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("GET /api/v3/qualityprofile against {BaseUrl} returned {StatusCode}", BaseUrl, response.StatusCode);
            return Array.Empty<ProfileDto>();
        }

        var array = await response.Content.ReadFromJsonAsync<JsonArray>(cancellationToken: cancellationToken).ConfigureAwait(false);
        var result = new List<ProfileDto>();
        if (array is null)
        {
            return result;
        }

        foreach (var node in array)
        {
            if (node is null)
            {
                continue;
            }

            result.Add(new ProfileDto
            {
                Id = node["id"]?.GetValue<int>() ?? 0,
                Name = node["name"]?.GetValue<string>() ?? string.Empty
            });
        }

        return result;
    }

    /// <summary>
    /// Builds an authenticated request against this instance.
    /// </summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="pathAndQuery">Path and query string, e.g. <c>/api/v3/movie?tmdbId=1</c>.</param>
    /// <returns>The request.</returns>
    protected HttpRequestMessage NewRequest(HttpMethod method, string pathAndQuery)
    {
        var request = new HttpRequestMessage(method, $"{BaseUrl.TrimEnd('/')}{pathAndQuery}");
        var apiKey = ApiKey;
        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Add("X-Api-Key", apiKey);
        }

        return request;
    }

    /// <summary>
    /// GETs a list endpoint expected to return zero or one match and returns the first.
    /// </summary>
    /// <param name="pathAndQuery">Path and query string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first matching record, or <c>null</c>.</returns>
    protected async Task<JsonObject?> FindSingleByQueryAsync(string pathAndQuery, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        using var request = NewRequest(HttpMethod.Get, pathAndQuery);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("GET {Path} against {BaseUrl} returned {StatusCode}", pathAndQuery, BaseUrl, response.StatusCode);
            return null;
        }

        var array = await response.Content.ReadFromJsonAsync<JsonArray>(cancellationToken: cancellationToken).ConfigureAwait(false);
        return array is { Count: > 0 } ? array[0]!.AsObject() : null;
    }

    /// <summary>
    /// Mutates only <c>qualityProfileId</c> on <paramref name="record"/> and PUTs the
    /// whole object back.
    /// </summary>
    /// <param name="resourcePath">The resource path, e.g. <c>/api/v3/movie/373</c>.</param>
    /// <param name="record">The full record previously read from the instance.</param>
    /// <param name="qualityProfileId">The new quality profile id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> on success.</returns>
    protected async Task<bool> UpdateQualityProfileAsync(string resourcePath, JsonObject record, int qualityProfileId, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return false;
        }

        record["qualityProfileId"] = qualityProfileId;

        using var request = NewRequest(HttpMethod.Put, resourcePath);
        request.Content = new StringContent(record.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("PUT {Path} against {BaseUrl} returned {StatusCode}", resourcePath, BaseUrl, response.StatusCode);
        }

        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Posts a command (e.g. a search) to <c>/api/v3/command</c>.
    /// </summary>
    /// <param name="commandBody">The command payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> on success.</returns>
    protected async Task<bool> TriggerCommandAsync(object commandBody, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return false;
        }

        using var request = NewRequest(HttpMethod.Post, "/api/v3/command");
        request.Content = JsonContent.Create(commandBody);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogWarning("POST /api/v3/command against {BaseUrl} returned {StatusCode}", BaseUrl, response.StatusCode);
        }

        return response.IsSuccessStatusCode;
    }
}
