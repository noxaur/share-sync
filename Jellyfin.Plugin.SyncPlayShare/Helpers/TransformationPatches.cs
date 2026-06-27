using System;
using System.Globalization;
using Jellyfin.Plugin.SyncPlayShare.Models;
using MediaBrowser.Common.Net;

namespace Jellyfin.Plugin.SyncPlayShare.Helpers;

/// <summary>
/// File Transformation callbacks.
/// </summary>
public static class TransformationPatches
{
    private const string ScriptMarker = "syncplay-share.js";

    /// <summary>
    /// Injects the SyncPlay Share script into jellyfin-web.
    /// </summary>
    /// <param name="payload">The File Transformation payload.</param>
    /// <returns>The transformed content.</returns>
    public static string IndexHtml(PatchRequestPayload payload)
    {
        Plugin? plugin = Plugin.Instance;
        string contents = payload.Contents ?? string.Empty;

        plugin?.LogVerbose("IndexHtml transformation callback entered.");

        if (plugin is null)
        {
            return contents;
        }

        if (!plugin.Configuration.Enabled)
        {
            plugin.LogDebug("Plugin disabled; leaving index.html unchanged.");
            return contents;
        }

        if (contents.Contains(ScriptMarker, StringComparison.Ordinal))
        {
            plugin.LogDebug("Script tag already present; leaving index.html unchanged.");
            return contents;
        }

        string rootPath = GetRootPath(plugin);
        string version = plugin.Version?.ToString() ?? "0.0.0.0";
        string scriptUrl = string.Format(
            CultureInfo.InvariantCulture,
            "{0}/SyncPlayShare/syncplay-share.js?v={1}",
            rootPath,
            Uri.EscapeDataString(version));
        string scriptTag = string.Format(
            CultureInfo.InvariantCulture,
            "<script type=\"text/javascript\" plugin=\"Jellyfin.Plugin.SyncPlayShare\" src=\"{0}\" defer></script>",
            scriptUrl);

        plugin.LogDebug("Injecting script: " + scriptUrl);
        plugin.LogVerbose("IndexHtml transformation callback exited.");

        int bodyIndex = contents.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyIndex < 0)
        {
            return contents + scriptTag;
        }

        return contents.Insert(bodyIndex, scriptTag);
    }

    private static string GetRootPath(Plugin plugin)
    {
        string? baseUrl = plugin.ServerConfigurationManager.GetNetworkConfiguration().BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return string.Empty;
        }

        return "/" + baseUrl.Trim('/');
    }
}
