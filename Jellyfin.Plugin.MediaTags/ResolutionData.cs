using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.MediaTags;

/// <summary>
/// Static class containing ISO language code data and utilities.
/// </summary>
public static class ResolutionData
{
    private static readonly Dictionary<string, ResolutionInfo> _languageDictionary = InitializeResolutionDictionary();

    /// <summary>
    /// Gets the language dictionary for fast lookups.
    /// </summary>
    public static Dictionary<string, ResolutionInfo> LanguageDictionary => _languageDictionary;

    /// <summary>
    /// Checks if a language code is valid (exists in the ISO standards).
    /// </summary>
    /// <param name="code">The language code to validate.</param>
    /// <returns>True if the code is a valid ISO language code, false otherwise.</returns>
    public static bool IsValidResolution(string? code)
    {
        return !string.IsNullOrEmpty(code) && _languageDictionary.ContainsKey(code);
    }

    /// <summary>
    /// Tries to get language information for a given code.
    /// </summary>
    /// <param name="code">The language code to look up.</param>
    /// <param name="languageInfo">The language information if found.</param>
    /// <returns>True if the language was found, false otherwise.</returns>
    public static bool TryGetLanguageInfo(string? code, out ResolutionInfo? languageInfo)
    {
        languageInfo = null;
        return !string.IsNullOrEmpty(code) && _languageDictionary.TryGetValue(code, out languageInfo);
    }

    private static Dictionary<string, ResolutionInfo> InitializeResolutionDictionary()
    {
        var languages = new Dictionary<string, ResolutionInfo>(StringComparer.OrdinalIgnoreCase);

        void AddResolution(ResolutionInfo res)
        {
        }

        // Add just one example language (German) as requested
        AddResolution(new ResolutionInfo { MinHeight = 1080, Name = "1080p" });
        AddResolution(new ResolutionInfo { MinHeight = 2160, Name = "4K" });
        AddResolution(new ResolutionInfo { MinHeight = 720, Name = "720p" });

        return languages;
    }
}
