using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.ArrProfileSwitcher.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ArrProfileSwitcher;

/// <summary>
/// Lets any Jellyfin user switch a movie's or show's Radarr/Sonarr quality profile,
/// from an admin-curated list of options, and trigger a search.
/// </summary>
/// <remarks>
/// Also serves the client-side script (menu entry + detail-page button) injected into
/// Jellyfin Web via the File Transformation plugin. See Services/StartupService.cs for
/// the registration and Configuration/arrprofileswitcher.html for the admin settings page.
/// </remarks>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Arr Profile Switcher";

    /// <inheritdoc />
    public override string Description =>
        "Lets any Jellyfin user switch a movie's or show's Radarr/Sonarr quality profile, " +
        "from an admin-curated list of options, and trigger a search.";

    /// <summary>
    /// Gets the plugin id. Must match <c>guid</c> in meta.json and build.yaml and
    /// the <c>pluginUniqueId</c> in the config page's JavaScript.
    /// </summary>
    public override Guid Id => Guid.Parse("6556f718-0762-48ec-bc64-bac5b86ed0c6");

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace;

        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.arrprofileswitcher.html",
                    ns)
            },
            // Served at /web/configurationpage?name=arrProfileSwitcher.js — same mechanism
            // WhisperSubs uses live on this server (confirmed via its own served index.html:
            // <script src="configurationpage?name=whisperSubs.js">) to ship a plugin's own
            // client script without a bespoke controller endpoint.
            new PluginPageInfo
            {
                Name = "arrProfileSwitcher.js",
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.ClientScript.arr-profile-switcher.js",
                    ns)
            },
            new PluginPageInfo
            {
                Name = "arrProfileSwitcher.css",
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.ClientScript.arr-profile-switcher.css",
                    ns)
            }
        };
    }
}
