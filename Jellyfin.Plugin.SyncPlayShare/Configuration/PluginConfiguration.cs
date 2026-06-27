using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SyncPlayShare.Configuration;

/// <summary>
/// The configuration options.
/// </summary>
public enum SyncPlayShareLogLevel
{
    /// <summary>
    /// Log errors only.
    /// </summary>
    Error = 0,

    /// <summary>
    /// Log normal lifecycle events.
    /// </summary>
    Info = 1,

    /// <summary>
    /// Log diagnostic events.
    /// </summary>
    Debug = 2,

    /// <summary>
    /// Log detailed transformation events.
    /// </summary>
    Verbose = 3
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        Enabled = true;
        LogLevel = SyncPlayShareLogLevel.Info;
        ClientConsoleLogging = false;
        CopyToastEnabled = true;
        ShareButtonLabel = "Share";
    }

    /// <summary>
    /// Gets or sets a value indicating whether SyncPlay sharing is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the server and client diagnostic log level.
    /// </summary>
    public SyncPlayShareLogLevel LogLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether client debug logs are written to the browser console.
    /// </summary>
    public bool ClientConsoleLogging { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether copy/join status toasts are shown.
    /// </summary>
    public bool CopyToastEnabled { get; set; }

    /// <summary>
    /// Gets or sets the share button label.
    /// </summary>
    public string ShareButtonLabel { get; set; }
}
