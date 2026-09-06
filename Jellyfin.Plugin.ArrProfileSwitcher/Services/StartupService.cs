using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.ArrProfileSwitcher.Services;

/// <summary>
/// Registers this plugin's client script injection with the File Transformation
/// plugin (IAmParadox27, <c>jellyfin-plugin-file-transformation</c>) at server startup.
/// </summary>
/// <remarks>
/// Plugins are loaded into separate <see cref="AssemblyLoadContext"/>s, so File
/// Transformation cannot be referenced directly — registration goes through
/// reflection against its <c>PluginInterface.RegisterTransformation(JObject)</c>
/// method, exactly as documented in that plugin's README and as used live by
/// <c>Jellyfin.Plugin.HomeScreenSections.Services.StartupService</c> (also installed
/// on this NUC's Jellyfin). Done as an <see cref="IScheduledTask"/> with a startup
/// trigger — not in <see cref="PluginServiceRegistrator"/> — because File
/// Transformation's own services may not be registered into Jellyfin's DI container
/// yet while plugins are still being loaded (same reasoning HomeScreenSections uses).
/// <para/>
/// If File Transformation isn't installed, this logs a warning and does nothing else
/// — the admin config page and the read/write API still work, only the client-side
/// picker never appears in the web UI.
/// </remarks>
public class StartupService : IScheduledTask
{
    /// <summary>
    /// The transformation id registered with File Transformation. Fixed so re-running
    /// this task (e.g. on every Jellyfin restart) re-registers idempotently rather
    /// than accumulating duplicate patches.
    /// </summary>
    private const string TransformationId = "326e4f91-9747-470b-9fb3-03be09d31842";

    private readonly ILogger<StartupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TCategoryName}"/> interface.</param>
    public StartupService(ILogger<StartupService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Arr Profile Switcher Startup";

    /// <inheritdoc />
    public string Key => "Jellyfin.Plugin.ArrProfileSwitcher.Startup";

    /// <inheritdoc />
    public string Description => "Registers the quality-profile picker's client script with the File Transformation plugin.";

    /// <inheritdoc />
    public string Category => "Startup Services";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        try
        {
            var fileTransformationAssembly = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .FirstOrDefault(assembly => assembly.FullName?.Contains(".FileTransformation") ?? false);

            if (fileTransformationAssembly is null)
            {
                _logger.LogWarning(
                    "File Transformation plugin not found; the quality-profile picker will not be injected " +
                    "into Jellyfin Web. Install https://github.com/IAmParadox27/jellyfin-plugin-file-transformation " +
                    "to enable it. The admin config page and API are unaffected.");
                return Task.CompletedTask;
            }

            var pluginInterfaceType = fileTransformationAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
            var registerMethod = pluginInterfaceType?.GetMethod("RegisterTransformation");
            if (registerMethod is null)
            {
                _logger.LogWarning(
                    "File Transformation plugin found but its PluginInterface.RegisterTransformation method " +
                    "could not be located (API may have changed) — client script not injected.");
                return Task.CompletedTask;
            }

            var payload = new JObject
            {
                ["id"] = TransformationId,
                ["fileNamePattern"] = "index.html",
                ["callbackAssembly"] = typeof(TransformationPatches).Assembly.FullName,
                ["callbackClass"] = typeof(TransformationPatches).FullName,
                ["callbackMethod"] = nameof(TransformationPatches.IndexHtml)
            };

            registerMethod.Invoke(null, new object?[] { payload });
            _logger.LogInformation("Registered index.html client-script injection with the File Transformation plugin.");
        }
        catch (Exception ex)
        {
            // Never let a registration failure take down plugin/server startup.
            _logger.LogError(ex, "Failed to register with the File Transformation plugin.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[] { new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger } };
    }
}
