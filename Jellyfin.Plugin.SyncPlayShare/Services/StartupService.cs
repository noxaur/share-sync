using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SyncPlayShare.Helpers;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SyncPlayShare.Services;

/// <summary>
/// Registers File Transformation hooks at startup.
/// </summary>
public class StartupService : IScheduledTask
{
    private readonly ILogger<StartupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public StartupService(ILogger<StartupService> logger)
    {
        _logger = logger;
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

        try
        {
            Assembly? fileTransformationAssembly = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .FirstOrDefault(assembly => assembly.FullName?.Contains(".FileTransformation", StringComparison.Ordinal) ?? false);

            if (fileTransformationAssembly is null)
            {
                plugin.LogError(null, "File Transformation plugin missing; install it and restart Jellyfin.");
                return Task.CompletedTask;
            }

            Type? pluginInterfaceType = fileTransformationAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
            MethodInfo? registerMethod = pluginInterfaceType?.GetMethod("RegisterTransformation");

            if (registerMethod is null)
            {
                plugin.LogError(null, "File Transformation RegisterTransformation method missing.");
                return Task.CompletedTask;
            }

            RegisterTransformation(
                registerMethod,
                plugin,
                "c282f8dd-2b02-45dd-b1b6-6c168b43c0a5",
                "^/?index\\.html$",
                nameof(TransformationPatches.IndexHtml));
        }
        catch (Exception ex)
        {
            plugin.LogError(ex, "Failed to register File Transformation.");
        }

        return Task.CompletedTask;
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

    private static void RegisterTransformation(
        MethodInfo registerMethod,
        Plugin plugin,
        string id,
        string fileNamePattern,
        string callbackMethod)
    {
        Dictionary<string, string?> payload = new Dictionary<string, string?>
        {
            ["id"] = id,
            ["fileNamePattern"] = fileNamePattern,
            ["callbackAssembly"] = typeof(StartupService).Assembly.FullName,
            ["callbackClass"] = typeof(TransformationPatches).FullName,
            ["callbackMethod"] = callbackMethod
        };
        Type payloadType = registerMethod.GetParameters()[0].ParameterType;
        MethodInfo? parseMethod = payloadType.GetMethod("Parse", [typeof(string)]);
        object? fileTransformationPayload = parseMethod?.Invoke(null, [JsonSerializer.Serialize(payload)]);

        if (fileTransformationPayload is null)
        {
            plugin.LogError(null, "Could not create File Transformation payload.");
            return;
        }

        registerMethod.Invoke(null, [fileTransformationPayload]);
        plugin.LogInfo("File Transformation registered for " + fileNamePattern + ".");
    }
}
