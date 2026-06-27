using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.SyncPlayShare.Models;

/// <summary>
/// Payload passed by File Transformation.
/// </summary>
public class PatchRequestPayload
{
    /// <summary>
    /// Gets or sets the file contents.
    /// </summary>
    [JsonPropertyName("contents")]
    public string? Contents { get; set; }
}
