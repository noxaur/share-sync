using System;
using System.Text.Json;
using Jellyfin.Plugin.SyncPlayShare.Configuration;

namespace Jellyfin.Plugin.SyncPlayShare.Helpers;

/// <summary>
/// Renders the browser script with plugin configuration.
/// </summary>
public static class PluginScriptRenderer
{
    /// <summary>
    /// Replaces the config placeholder in the script.
    /// </summary>
    /// <param name="script">The raw script.</param>
    /// <param name="configuration">The plugin configuration.</param>
    /// <returns>The rendered script.</returns>
    public static string Render(string script, PluginConfiguration configuration)
    {
        object config = new
        {
            enabled = configuration.Enabled,
            logLevel = configuration.LogLevel.ToString(),
            clientConsoleLogging = configuration.ClientConsoleLogging,
            copyToastEnabled = configuration.CopyToastEnabled,
            shareButtonLabel = configuration.ShareButtonLabel ?? "Share"
        };

        return script.Replace(
            "__SYNCPLAY_SHARE_CONFIG__",
            JsonSerializer.Serialize(config),
            StringComparison.Ordinal);
    }
}
