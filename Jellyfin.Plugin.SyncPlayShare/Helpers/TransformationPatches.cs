using System;
using System.Globalization;
using Jellyfin.Plugin.SyncPlayShare.Models;

namespace Jellyfin.Plugin.SyncPlayShare.Helpers;

/// <summary>
/// File Transformation callbacks.
/// </summary>
public static class TransformationPatches
{
    private const string InlineMarker = "Jellyfin.Plugin.SyncPlayShare injected";
    private const string ChunkLoader =
        ";(function(w,d){if(w.SyncPlayShareLoaded||w.syncplayShareLoading)return;w.syncplayShareLoading=1;var s=d.createElement('script');s.src='/SyncPlayShare/syncplay-share.js';d.head.appendChild(s);})(window,document);";

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

        if (plugin is not null && !plugin.Configuration.Enabled)
        {
            plugin.LogDebug("Plugin disabled; leaving index.html unchanged.");
            return contents;
        }

        if (!IsHtmlDocument(contents))
        {
            plugin?.LogDebug("Payload is not an HTML document; leaving unchanged.");
            return contents;
        }

        if (contents.Contains(InlineMarker, StringComparison.Ordinal))
        {
            plugin?.LogDebug("Inline script already present; leaving index.html unchanged.");
            return contents;
        }

        string scriptTag = string.Format(
            CultureInfo.InvariantCulture,
            "<!-- {0} --><script defer=\"defer\" type=\"text/javascript\" plugin=\"Jellyfin.Plugin.SyncPlayShare\" src=\"/SyncPlayShare/syncplay-share.js\"></script>",
            InlineMarker);

        plugin?.LogDebug("Injecting SyncPlay Share script into index.html.");
        plugin?.LogVerbose("IndexHtml transformation callback exited.");

        int bodyIndex = contents.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyIndex < 0)
        {
            return contents + scriptTag;
        }

        return contents.Insert(bodyIndex, scriptTag);
    }

    /// <summary>
    /// Appends a SyncPlay Share loader to the lazy SyncPlay chunk.
    /// </summary>
    /// <param name="payload">The File Transformation payload.</param>
    /// <returns>The transformed content.</returns>
    public static string SyncPlayChunk(PatchRequestPayload payload)
    {
        try
        {
            return SyncPlayChunkCore(payload);
        }
        catch (Exception ex)
        {
            Plugin.Instance?.LogError(ex, "SyncPlayChunk transformation failed; leaving payload unchanged.");
            return payload?.Contents ?? string.Empty;
        }
    }

    private static string SyncPlayChunkCore(PatchRequestPayload payload)
    {
        Plugin? plugin = Plugin.Instance;
        string contents = payload?.Contents ?? string.Empty;

        if (plugin is not null && !plugin.Configuration.Enabled)
        {
            return contents;
        }

        if (contents.Contains(InlineMarker, StringComparison.Ordinal))
        {
            return contents;
        }

        plugin?.LogDebug("Appending SyncPlay Share loader to SyncPlay chunk.");
        return contents + "\n/* " + InlineMarker + " */\n" + ChunkLoader;
    }

    private static bool IsHtmlDocument(string contents)
    {
        return contents.Contains("<!doctype html", StringComparison.OrdinalIgnoreCase)
            || contents.Contains("<html", StringComparison.OrdinalIgnoreCase)
            || contents.Contains("<body", StringComparison.OrdinalIgnoreCase);
    }
}
