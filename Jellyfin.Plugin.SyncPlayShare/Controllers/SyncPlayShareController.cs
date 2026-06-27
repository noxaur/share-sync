using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SyncPlayShare.Controllers;

/// <summary>
/// Serves SyncPlay Share web assets.
/// </summary>
[ApiController]
[Route("[controller]")]
public class SyncPlayShareController : ControllerBase
{
    /// <summary>
    /// Gets the client script.
    /// </summary>
    /// <returns>The script file.</returns>
    [HttpGet("syncplay-share.js")]
    [Produces("application/javascript")]
    public ActionResult GetScript()
    {
        Plugin? plugin = Plugin.Instance;
        plugin?.LogInfo("Script served.");

        Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(typeof(Plugin).Namespace + ".Assets.syncplay-share.js");

        if (stream is null)
        {
            plugin?.LogError(null, "Embedded syncplay-share.js missing.");
            return NotFound();
        }

        string script;
        using (stream)
        using (StreamReader reader = new StreamReader(stream))
        {
            script = reader.ReadToEnd();
        }

        if (plugin?.Configuration.Enabled == true)
        {
            Response.Headers.CacheControl = "public, max-age=3600";
        }
        else
        {
            Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        }

        object config = new
        {
            enabled = plugin?.Configuration.Enabled ?? false,
            logLevel = plugin?.Configuration.LogLevel.ToString() ?? "Error",
            clientConsoleLogging = plugin?.Configuration.ClientConsoleLogging ?? false,
            copyToastEnabled = plugin?.Configuration.CopyToastEnabled ?? true,
            shareButtonLabel = plugin?.Configuration.ShareButtonLabel ?? "Share"
        };

        script = script.Replace(
            "__SYNCPLAY_SHARE_CONFIG__",
            JsonSerializer.Serialize(config),
            StringComparison.Ordinal);

        return Content(script, "application/javascript");
    }
}
