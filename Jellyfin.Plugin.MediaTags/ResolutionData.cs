using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.MediaTags;

/// <summary>
/// Static class containing ISO language code data and utilities.
/// </summary>
public static class ResolutionData
{
    private static readonly List<ResolutionInfo> _languageDictionary = InitializeResolutionList();

    /// <summary>
    /// Gets the language dictionary for fast lookups.
    /// </summary>
    public static List<ResolutionInfo> ResolutionList => _languageDictionary;

    /// <summary>
    /// Checks if a language code is valid (exists in the ISO standards).
    /// </summary>
    /// <param name="name">The language code to validate.</param>
    /// <returns>True if the code is a valid ISO language code, false otherwise.</returns>
    public static bool IsValidResolution(string? name)
    {
        return !string.IsNullOrEmpty(name) && _languageDictionary.Exists(r => r.Name.Equals(name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Tries to get language information for a given code.
    /// </summary>
    /// <param name="code">The language code to look up.</param>
    /// <param name="resolutionInfo">The language information if found.</param>
    /// <returns>True if the language was found, false otherwise.</returns>
    public static bool TryGetResolutionInfo(string? code, out ResolutionInfo? resolutionInfo)
    {
        resolutionInfo = null;
        return !string.IsNullOrEmpty(code) && _languageDictionary.Exists(r => r.Name.Equals(code, StringComparison.Ordinal));
    }

    private static List<ResolutionInfo> InitializeResolutionList()
    {
        var languages = new List<ResolutionInfo>();

        void AddResolution(ResolutionInfo res)
        {
            languages.Add(res);
        }

        // Add just one example language (German) as requested
        AddResolution(new ResolutionInfo { MaxHeight = 2160, MaxWidth = 3840, Name = "4K" });
        AddResolution(new ResolutionInfo { MaxHeight = 1080, MaxWidth = 1920, Name = "1080p" });
        AddResolution(new ResolutionInfo { MaxHeight = 720, MaxWidth = 1280, Name = "720p" });

        return languages;
    }
}
