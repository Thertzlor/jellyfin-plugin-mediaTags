using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MediaTags.Configuration;

/// <summary>
/// Class holding the plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration" /> class.
    /// </summary>
    public PluginConfiguration()
    {
        AlwaysForceFullRefresh = false;
        WhitelistLanguageTags = string.Empty;
        AddHdrTags = false;
        SynchronousRefresh = false;
        DisableUndefinedLanguageTags = false;
        ResolutionTagPrefix = "language_";
        HdrTagPrefix = "subtitle_language_";
        EnableNonMediaTagging = false;
        NonMediaTag = "item";
        NonMediaItemTypes = string.Empty;
        TagSeriesOnly = false;
    }

    /// <summary>
    /// Gets or sets a value indicating whether to always force a full refresh.
    /// </summary>
    public bool AlwaysForceFullRefresh { get; set; }

    /// <summary>
    /// Gets or sets the whitelist of language tags.
    /// </summary>
    public string WhitelistLanguageTags { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to extract subtitle languages.
    /// </summary>
    public bool AddHdrTags { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to refresh synchronously.
    /// </summary>
    public bool SynchronousRefresh { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to disable adding 'und' tags for undefined languages.
    /// </summary>
    public bool DisableUndefinedLanguageTags { get; set; }

    /// <summary>
    /// Gets or sets the prefix for Resolution tags.
    /// </summary>
    public string ResolutionTagPrefix { get; set; }

    /// <summary>
    /// Gets or sets the prefix for HDR type tags.
    /// </summary>
    public string HdrTagPrefix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable non-media tagging.
    /// </summary>
    public bool EnableNonMediaTagging { get; set; }

    /// <summary>
    /// Gets or sets the tag to apply to non-media items.
    /// </summary>
    public string NonMediaTag { get; set; }

    /// <summary>
    /// Gets or sets which non-media item types to tag (comma-separated).
    /// </summary>
    public string NonMediaItemTypes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to tag only the root Series, skipping Seasons and Episodes.
    /// </summary>
    public bool TagSeriesOnly { get; set; }
}
