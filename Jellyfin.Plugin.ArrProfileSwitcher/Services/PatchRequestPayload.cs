using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Services;

/// <summary>
/// The object the File Transformation plugin invokes a registered callback with.
/// </summary>
/// <remarks>
/// This is NOT a Jellyfin SDK type — File Transformation's README only documents the
/// JSON shape (<c>{ "contents": "..." }</c>), and every plugin that integrates with it
/// (confirmed across a dozen+ public plugin repos, e.g.
/// <c>Jellyfin.Plugin.PluginPages.Model.PatchRequestPayload</c> from the same author as
/// File Transformation itself) defines this POCO locally rather than depending on a
/// shared package. Field name/casing matches that reference exactly.
/// </remarks>
public class PatchRequestPayload
{
    /// <summary>
    /// Gets or sets the current state of the file being requested.
    /// </summary>
    [JsonPropertyName("contents")]
    public string? Contents { get; set; }
}
