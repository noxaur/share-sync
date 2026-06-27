using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SyncPlayShare.Helpers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SyncPlayShare.Services;

/// <summary>
/// Registers File Transformation hooks at startup.
/// </summary>
public class StartupService : IScheduledTask
{
    private static readonly object RegistrationSync = new object();
    private static bool _registered;
    private readonly ILogger<StartupService> _logger;
    private readonly IApplicationPaths _applicationPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="applicationPaths">The application paths.</param>
    public StartupService(ILogger<StartupService> logger, IApplicationPaths applicationPaths)
    {
        _logger = logger;
        _applicationPaths = applicationPaths;
    }

    /// <inheritdoc />
    public string Name => "SyncPlay Share Startup";

    /// <inheritdoc />
    public string Key => "Jellyfin.Plugin.SyncPlayShare.Startup";

    /// <inheritdoc />
    public string Description => "Registers SyncPlay Share web transformations.";

    /// <inheritdoc />
    public string Category => "Startup Services";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        Plugin? plugin = Plugin.Instance;
        plugin?.SetLogger(_logger);

        if (plugin is null)
        {
            _logger.LogError("[SyncPlayShare] Plugin instance missing; cannot register transformation.");
            return Task.CompletedTask;
        }

        plugin.LogInfo("Config loaded.");
        plugin.LogDebug("Enabled=" + plugin.Configuration.Enabled + ", LogLevel=" + plugin.Configuration.LogLevel + ", ClientConsoleLogging=" + plugin.Configuration.ClientConsoleLogging + ", CopyToastEnabled=" + plugin.Configuration.CopyToastEnabled + ", ShareButtonLabel=" + plugin.Configuration.ShareButtonLabel);

        RegisterTransformations(plugin, _applicationPaths);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers the web transformations.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="applicationPaths">Optional application paths for chunk discovery.</param>
    public static void RegisterTransformations(Plugin plugin, IApplicationPaths? applicationPaths = null)
    {
        lock (RegistrationSync)
        {
            if (_registered)
            {
                return;
            }

            try
            {
                Assembly? fileTransformationAssembly = AssemblyLoadContext.All
                    .SelectMany(context => context.Assemblies)
                    .FirstOrDefault(assembly => assembly.FullName?.Contains(".FileTransformation", StringComparison.Ordinal) ?? false);

                if (fileTransformationAssembly is null)
                {
                    plugin.LogError(null, "File Transformation plugin missing; install it and restart Jellyfin.");
                    return;
                }

                Type? pluginInterfaceType = fileTransformationAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
                MethodInfo? registerMethod = pluginInterfaceType?.GetMethod("RegisterTransformation");

                if (registerMethod is null)
                {
                    plugin.LogError(null, "File Transformation RegisterTransformation method missing.");
                    return;
                }

                if (!RegisterTransformation(
                        registerMethod,
                        plugin,
                        "c282f8dd-2b02-45dd-b1b6-6c168b43c0a5",
                        "(^|[\\\\/])index\\.html$",
                        "/SyncPlayShare/transform/index-html"))
                {
                    return;
                }

                RegisterSyncPlayChunkTransformations(registerMethod, plugin, applicationPaths);
                _registered = true;
            }
            catch (Exception ex)
            {
                plugin.LogError(ex, "Failed to register File Transformation.");
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.StartupTrigger
            }
        ];
    }

    private static bool RegisterTransformation(
        MethodInfo registerMethod,
        Plugin plugin,
        string id,
        string fileNamePattern,
        string transformationEndpoint)
    {
        Dictionary<string, string?> payload = new Dictionary<string, string?>
        {
            ["id"] = id,
            ["fileNamePattern"] = fileNamePattern,
            ["transformationEndpoint"] = transformationEndpoint
        };
        Type payloadType = registerMethod.GetParameters()[0].ParameterType;
        MethodInfo? parseMethod = payloadType.GetMethod("Parse", [typeof(string)]);
        object? fileTransformationPayload = parseMethod?.Invoke(null, [JsonSerializer.Serialize(payload)]);

        if (fileTransformationPayload is null)
        {
            plugin.LogError(null, "Could not create File Transformation payload for " + fileNamePattern + ".");
            return false;
        }

        registerMethod.Invoke(null, [fileTransformationPayload]);
        plugin.LogInfo("File Transformation registered for " + fileNamePattern + " via " + transformationEndpoint + ".");
        return true;
    }

    private static void RegisterSyncPlayChunkTransformations(
        MethodInfo registerMethod,
        Plugin plugin,
        IApplicationPaths? applicationPaths)
    {
        if (applicationPaths is null
            || string.IsNullOrWhiteSpace(applicationPaths.WebPath)
            || !Directory.Exists(applicationPaths.WebPath))
        {
            plugin.LogDebug("Jellyfin web path missing; skipping SyncPlay chunk transformation.");
            return;
        }

        Regex chunkName = new Regex(@"([^.]+)\.[^.]+\.chunk\.js", RegexOptions.Compiled);
        foreach (string jsChunk in Directory.GetFiles(applicationPaths.WebPath, "*.chunk.js", SearchOption.AllDirectories))
        {
            string chunkContents = File.ReadAllText(jsChunk);
            if (!chunkContents.Contains("halt-playback", StringComparison.Ordinal)
                || !chunkContents.Contains("leave-group", StringComparison.Ordinal)
                || !chunkContents.Contains("settings", StringComparison.Ordinal))
            {
                continue;
            }

            string fileName = Path.GetFileName(jsChunk);
            Match match = chunkName.Match(fileName);
            if (!match.Success)
            {
                continue;
            }

            string fileNamePattern = match.Groups[1].Value + "\\.[^.]+\\.chunk\\.js";
            plugin.LogInfo("Found SyncPlay menu chunk " + fileName + ".");
            RegisterTransformation(
                registerMethod,
                plugin,
                Guid.NewGuid().ToString(),
                fileNamePattern,
                "/SyncPlayShare/transform/syncplay-chunk");
        }
    }
}
