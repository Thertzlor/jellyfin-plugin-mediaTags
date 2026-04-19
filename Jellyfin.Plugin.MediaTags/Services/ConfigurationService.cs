using System;
using Jellyfin.Plugin.MediaTags.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaTags.Services;

/// <summary>
/// Service for accessing plugin configuration with validation.
/// </summary>
public class ConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the logger.</param>
    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
    }

    private PluginConfiguration Config => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    /// <summary>
    /// Gets a value indicating whether full refresh should always be forced.
    /// </summary>
    public bool AlwaysForceFullRefresh => Config.AlwaysForceFullRefresh;

    /// <summary>
    /// Gets a value indicating whether synchronous refresh is enabled.
    /// </summary>
    public bool SynchronousRefresh => Config.SynchronousRefresh;

    /// <summary>
    /// Gets a value indicating whether HDR range tags should be added.
    /// </summary>
    public bool AddHdrTags => Config.AddHdrTags;

    /// <summary>
    /// Gets a value indicating whether HDR range tags should be added.
    /// </summary>
    public bool AddHdrPlusTags => Config.AddHdrPlusTags;

    /// <summary>
    /// Gets a value indicating whether HDR range tags should be added.
    /// </summary>
    public bool AddDVTags => Config.AddDVTags;

    /// <summary>
    /// Gets a value indicating whether HDR range tags should be added.
    /// </summary>
    public bool AddDVPTags => Config.AddDVPTags;

    /// <summary>
    /// Gets a value indicating whether non-media tagging is enabled.
    /// </summary>
    public bool EnableNonMediaTagging => Config.EnableNonMediaTagging;

    /// <summary>
    /// Gets the non-media tag name.
    /// </summary>
    public string NonMediaTag => Config.NonMediaTag ?? "item";

    /// <summary>
    /// Gets the non-media item types.
    /// </summary>
    public string NonMediaItemTypes => Config.NonMediaItemTypes ?? string.Empty;

    /// <summary>
    /// Gets the whitelist of media tags.
    /// </summary>
    public string WhitelistTags => Config.WhitelistTags ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether to tag only the root Series, skipping Seasons and Episodes.
    /// </summary>
    public bool TagSeriesOnly => Config.TagSeriesOnly;

    /// <summary>
    /// Gets the validated media tag prefix.
    /// </summary>
    /// <returns>The validated media tag prefix.</returns>
    public string GetResolutionPrefix()
        => GetValidatedPrefix(Config.ResolutionTagPrefix, Config.HdrTagPrefix, "res_", "resolution");

    /// <summary>
    /// Gets the validated range tag prefix.
    /// </summary>
    /// <returns>The validated range tag prefix.</returns>
    public string GetHdrTypePrefix()
        => GetValidatedPrefix(Config.HdrTagPrefix, Config.ResolutionTagPrefix, "range_", "color range");

    /// <summary>
    /// Validates a prefix and ensures it's different from the other prefix.
    /// </summary>
    /// <param name="prefix">The prefix to validate.</param>
    /// <param name="otherPrefix">The other prefix to compare against.</param>
    /// <param name="defaultPrefix">The default prefix to use if validation fails.</param>
    /// <param name="prefixType">The type of prefix for logging.</param>
    /// <returns>The validated prefix.</returns>
    private string GetValidatedPrefix(string prefix, string otherPrefix, string defaultPrefix, string prefixType)
    {
        // Validate prefix: must be at least 3 characters
        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length < 3)
        {
            return defaultPrefix;
        }

        // Ensure prefixes are different
        if (!string.IsNullOrWhiteSpace(otherPrefix) && prefix.Equals(otherPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Resolution and color range prefixes cannot be identical. Using default {PrefixType} prefix '{DefaultPrefix}'",
                prefixType,
                defaultPrefix);
            return defaultPrefix;
        }

        return prefix;
    }
}
