using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.SyncPlayShare.Configuration;
using Jellyfin.Plugin.SyncPlayShare.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SyncPlayShare;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="serverConfigurationManager">Instance of the <see cref="IServerConfigurationManager"/> interface.</param>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        IServerConfigurationManager serverConfigurationManager)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        ServerConfigurationManager = serverConfigurationManager;
        StartupService.RegisterTransformations(this);
    }

    /// <inheritdoc />
    public override string Name => "SyncPlay Share";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("43f7bc73-726d-4f80-8f26-626a7b4d4b0f");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the server configuration manager.
    /// </summary>
    public IServerConfigurationManager ServerConfigurationManager { get; }

    /// <summary>
    /// Stores a logger for static callbacks invoked by File Transformation.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Logs an error.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="message">The message template.</param>
    public void LogError(Exception? exception, string message)
    {
        _logger?.LogError(exception, "[SyncPlayShare] {Message}", message);
    }

    /// <summary>
    /// Logs an informational event when enabled.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogInfo(string message)
    {
        if (Configuration.LogLevel >= SyncPlayShareLogLevel.Info)
        {
            _logger?.LogInformation("[SyncPlayShare] {Message}", message);
        }
    }

    /// <summary>
    /// Logs a debug event when enabled.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogDebug(string message)
    {
        if (Configuration.LogLevel >= SyncPlayShareLogLevel.Debug)
        {
            _logger?.LogDebug("[SyncPlayShare] {Message}", message);
        }
    }

    /// <summary>
    /// Logs a verbose event when enabled.
    /// </summary>
    /// <param name="message">The message.</param>
    public void LogVerbose(string message)
    {
        if (Configuration.LogLevel >= SyncPlayShareLogLevel.Verbose)
        {
            _logger?.LogTrace("[SyncPlayShare] {Message}", message);
        }
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        ];
    }
}
