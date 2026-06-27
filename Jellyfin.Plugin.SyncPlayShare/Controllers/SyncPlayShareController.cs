using System;
using System.IO;
using System.Reflection;
using Jellyfin.Plugin.SyncPlayShare.Helpers;
using Jellyfin.Plugin.SyncPlayShare.Models;
using Jellyfin.Plugin.SyncPlayShare.Services;
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
        if (plugin is not null)
        {
            StartupService.RegisterTransformations(plugin, plugin.ServerConfigurationManager.ApplicationPaths);
        }

        Response.Headers["X-SyncPlayShare-Version"] = typeof(Plugin).Assembly.GetName().Version?.ToString();

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

        script = plugin is null ? script : PluginScriptRenderer.Render(script, plugin.Configuration);

        return Content(script, "application/javascript");
    }

    /// <summary>
    /// Transforms index.html for File Transformation.
    /// </summary>
    /// <param name="payload">The file contents.</param>
    /// <returns>The transformed HTML.</returns>
    [HttpPost("transform/index-html")]
    [Produces("text/html")]
    public ActionResult TransformIndexHtml([FromBody] PatchRequestPayload payload)
    {
        Plugin.Instance?.LogDebug("IndexHtml transform endpoint called.");
        return Content(TransformationPatches.IndexHtml(payload), "text/html");
    }

    /// <summary>
    /// Transforms the SyncPlay web chunk for File Transformation.
    /// </summary>
    /// <param name="payload">The file contents.</param>
    /// <returns>The transformed JavaScript.</returns>
    [HttpPost("transform/syncplay-chunk")]
    [Produces("application/javascript")]
    public ActionResult TransformSyncPlayChunk([FromBody] PatchRequestPayload payload)
    {
        Plugin.Instance?.LogDebug("SyncPlayChunk transform endpoint called.");
        return Content(TransformationPatches.SyncPlayChunk(payload), "application/javascript");
    }
}
