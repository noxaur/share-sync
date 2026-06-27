using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Jellyfin.Plugin.SyncPlayShare.Models;
using MediaBrowser.Common.Net;

namespace Jellyfin.Plugin.SyncPlayShare.Helpers;

/// <summary>
/// File Transformation callbacks.
/// </summary>
public static class TransformationPatches
{
    private const string InlineMarker = "Jellyfin.Plugin.SyncPlayShare injected";

    /// <summary>
    /// Injects the SyncPlay Share script into jellyfin-web.
    /// </summary>
    /// <param name="payload">The File Transformation payload.</param>
    /// <returns>The transformed content.</returns>
    public static string IndexHtml(PatchRequestPayload payload)
    {
        try
        {
            return IndexHtmlCore(payload);
        }
        catch (Exception ex)
        {
            Plugin.Instance?.LogError(ex, "IndexHtml transformation failed; leaving payload unchanged.");
            return payload?.Contents ?? string.Empty;
        }
    }

    private static string IndexHtmlCore(PatchRequestPayload payload)
    {
        Plugin? plugin = Plugin.Instance;
        string contents = payload?.Contents ?? string.Empty;

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

        if (!IsHtmlDocument(contents))
        {
            plugin.LogDebug("Payload is not an HTML document; leaving unchanged.");
            return contents;
        }

        if (contents.Contains(InlineMarker, StringComparison.Ordinal))
        {
            plugin.LogDebug("Inline script already present; leaving index.html unchanged.");
            return contents;
        }

        string scriptTag = string.Format(
            CultureInfo.InvariantCulture,
            "<!-- {0} --><script type=\"text/javascript\" plugin=\"Jellyfin.Plugin.SyncPlayShare\">{1}</script>",
            InlineMarker,
            GetClientScript(plugin));

        plugin.LogDebug("Injecting inline script into index.html.");
        plugin.LogVerbose("IndexHtml transformation callback exited.");

        int bodyIndex = contents.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyIndex < 0)
        {
            return contents + scriptTag;
        }

        return contents.Insert(bodyIndex, scriptTag);
    }

    private static bool IsHtmlDocument(string contents)
    {
        return contents.Contains("<!doctype html", StringComparison.OrdinalIgnoreCase)
            || contents.Contains("<html", StringComparison.OrdinalIgnoreCase)
            || contents.Contains("<body", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetClientScript(Plugin plugin)
    {
        Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(typeof(Plugin).Namespace + ".Assets.syncplay-share.js");

        if (stream is null)
        {
            plugin.LogError(null, "Embedded syncplay-share.js missing.");
            return string.Empty;
        }

        using (stream)
        using (StreamReader reader = new StreamReader(stream))
        {
            return PluginScriptRenderer.Render(reader.ReadToEnd(), plugin.Configuration);
        }
    }
}
