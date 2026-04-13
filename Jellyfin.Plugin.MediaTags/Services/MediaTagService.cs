using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaTags.Services;

/// <summary>
/// Type of language tag (audio or subtitle).
/// </summary>
public enum TagType
{
    /// <summary>
    /// Audio language tag.
    /// </summary>
    Resolution,

    /// <summary>
    /// Subtitle language tag.
    /// </summary>
    Hdr
}

/// <summary>
/// Service for managing language tags on library items.
/// </summary>
public class MediaTagService
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<MediaTagService> _logger;
    private readonly ConfigurationService _configService;
    private static readonly char[] Separator = new[] { ',' };

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaTagService"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the library manager.</param>
    /// <param name="logger">Instance of the logger.</param>
    /// <param name="configService">Instance of the configuration service.</param>
    public MediaTagService(
        ILibraryManager libraryManager,
        ILogger<MediaTagService> logger,
        ConfigurationService configService)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// Checks if an item has language tags of the specified type.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <param name="type">The tag type (Audio or Subtitle).</param>
    /// <param name="resolutionPrefix">The audio prefix to use.</param>
    /// <param name="hdrPrefix">The subtitle prefix to use.</param>
    /// <returns>True if the item has language tags of the specified type.</returns>
    public bool HasResolutionTags(BaseItem item, TagType type, string resolutionPrefix, string hdrPrefix)
    {
        var prefix = type == TagType.Resolution ? resolutionPrefix : hdrPrefix;
        return item.Tags.Any(tag => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets language tags from an item for the specified type.
    /// </summary>
    /// <param name="item">The item to get tags from.</param>
    /// <param name="type">The tag type (Audio or Subtitle).</param>
    /// <param name="resolutionPrefix">The audio prefix to use.</param>
    /// <param name="hdrPrefix">The subtitle prefix to use.</param>
    /// <returns>List of language tags.</returns>
    public List<string> GetResolutionTags(BaseItem item, TagType type, string resolutionPrefix, string hdrPrefix)
    {
        var prefix = type == TagType.Resolution ? resolutionPrefix : hdrPrefix;
        return item.Tags.Where(tag => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Removes language tags from an item for the specified type.
    /// </summary>
    /// <param name="item">The item to remove tags from.</param>
    /// <param name="type">The tag type (Audio or Subtitle).</param>
    /// <param name="resolutionPrefix">The audio prefix to use.</param>
    /// <param name="hdrPrefix">The subtitle prefix to use.</param>
    public void RemoveMediaTags(BaseItem item, TagType type, string resolutionPrefix, string hdrPrefix)
    {
        var prefix = type == TagType.Resolution ? resolutionPrefix : hdrPrefix;
        var tagsToRemove = item.Tags.Where(tag => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

        if (tagsToRemove.Count > 0)
        {
            item.Tags = item.Tags.Except(tagsToRemove, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    /// <summary>
    /// Adds language tags to an item with provided prefixes and whitelist.
    /// </summary>
    /// <param name="item">The item to add tags to.</param>
    /// <param name="tags">List of languages.</param>
    /// <param name="type">The tag type (Audio or Subtitle).</param>
    /// <param name="resolutionPrefix">The audio prefix to use.</param>
    /// <param name="hdrPrefix">The subtitle prefix to use.</param>
    /// <param name="whitelist">The whitelist to use for filtering.</param>
    /// <returns>List of added languages.</returns>
    public List<string> AddMediaTags(BaseItem item, List<string> tags, TagType type, string resolutionPrefix, string hdrPrefix, List<string> whitelist)
    {
        // Make sure languages are unique
        tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).Intersect(whitelist).ToList();
        var prefix = type == TagType.Resolution ? resolutionPrefix : hdrPrefix;

        var newAddedTags = new List<string>();
        foreach (var resolutionName in tags)
        {
            string tag = $"{prefix}{resolutionName}";
            if (!item.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                item.AddTag(tag);
                newAddedTags.Add(resolutionName);
            }
        }

        return newAddedTags;
    }

    /// <summary>
    /// Strips the tag prefix from a list of tags for the specified type.
    /// </summary>
    /// <param name="tags">The tags to strip the prefix from.</param>
    /// <param name="type">The tag type to get the prefix for.</param>
    /// <param name="resolutionPrefix">The audio prefix to use.</param>
    /// <param name="hdrPrefix">The subtitle prefix to use.</param>
    /// <returns>List of tags without the prefix.</returns>
    public List<string> StripTagPrefix(IEnumerable<string> tags, TagType type, string resolutionPrefix, string hdrPrefix)
    {
        var prefix = type == TagType.Resolution ? resolutionPrefix : hdrPrefix;
        return tags
            .Where(tag => tag.Length > prefix.Length)
            .Select(tag => tag.Substring(prefix.Length))
            .ToList();
    }

    /// <summary>
    /// Filters out languages based on provided whitelist.
    /// </summary>
    /// <param name="item">The item being processed (for logging).</param>
    /// <param name="tags">List of language ISO codes to filter.</param>
    /// <param name="whitelist">The whitelist to use.</param>
    /// <returns>Filtered list of language ISO codes.</returns>
    public List<string> FilterOutTags(BaseItem item, List<string> tags, List<string> whitelist)
    {
        if (whitelist.Count == 0)
        {
            return tags;
        }

        var filteredOutTags = tags.Except(whitelist).ToList();
        var filteredTags = tags.Intersect(whitelist).ToList();

        if (filteredOutTags.Count > 0)
        {
            _logger.LogInformation(
                "Filtered out languages for {ItemName}: {Languages}",
                item.Name,
                string.Join(", ", filteredOutTags));
        }

        return filteredTags;
    }

    /// <summary>
    /// Parses and validates a whitelist string.
    /// </summary>
    /// <param name="whitelistString">The whitelist string to parse.</param>
    /// <returns>List of valid language codes.</returns>
    public static List<string> ParseWhitelist(string whitelistString)
    {
        if (string.IsNullOrWhiteSpace(whitelistString))
        {
            return new List<string>();
        }

        var undArray = new[] { "und" };
        return whitelistString.Split(Separator, StringSplitOptions.RemoveEmptyEntries)
            .Select(lang => lang.Trim())
            .Where(lang => lang.Length == 3) // Valid ISO 639-2/B codes
            .Distinct()
            .Concat(undArray) // Always include "undefined"
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Adds audio language tags to an item, or undefined tag if no languages provided, using provided prefixes and whitelist.
    /// </summary>
    /// <param name="item">The item to add tags to.</param>
    /// <param name="resolutions">List of audio language ISO codes.</param>
    /// <param name="resolutionPrefix">The audio prefix to use.</param>
    /// <param name="hdrPrefix">The subtitle prefix to use.</param>
    /// <param name="whitelist">The whitelist to use for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of added language ISO codes.</returns>
    public async Task<List<string>> AddMediaTagsOrUndefined(BaseItem item, List<string> resolutions, string resolutionPrefix, string hdrPrefix, List<string> whitelist, CancellationToken cancellationToken)
    {
        if (resolutions.Count > 0)
        {
            return await Task.Run(() => AddMediaTags(item, resolutions, TagType.Resolution, resolutionPrefix, hdrPrefix, whitelist), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning("No audio language information found for {ItemName}, skipped adding undefined tags", item.Name);
        }

        return resolutions;
    }
}
