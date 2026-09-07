namespace Jellyfin.Plugin.ArrProfileSwitcher.Services;

/// <summary>
/// Callback methods invoked by the File Transformation plugin (IAmParadox27,
/// <c>jellyfin-plugin-file-transformation</c>) to patch <c>index.html</c> as Jellyfin
/// serves it. Registered by <see cref="StartupService"/>.
/// </summary>
/// <remarks>
/// Mirrors the same registration + patch-method shape used by
/// <c>Jellyfin.Plugin.HomeScreenSections.Helpers.TransformationPatches.IndexHtml</c>
/// (a popular community plugin) - confirmed via that plugin's public source.
/// </remarks>
public static class TransformationPatches
{
    /// <summary>
    /// Appends this plugin's client script and stylesheet before <c>&lt;/body&gt;</c>.
    /// </summary>
    /// <param name="content">The current state of <c>index.html</c>, from File Transformation.</param>
    /// <returns>The patched HTML.</returns>
    public static string IndexHtml(PatchRequestPayload content)
    {
        const string Style = "<link rel=\"stylesheet\" href=\"configurationpage?name=arrProfileSwitcher.css\" />";
        const string Script = "<script type=\"text/javascript\" plugin=\"Jellyfin.Plugin.ArrProfileSwitcher\" " +
            "src=\"configurationpage?name=arrProfileSwitcher.js\" defer></script>";

        return content.Contents!
            .Replace("</head>", Style + "</head>")
            .Replace("</body>", Script + "</body>");
    }
}
