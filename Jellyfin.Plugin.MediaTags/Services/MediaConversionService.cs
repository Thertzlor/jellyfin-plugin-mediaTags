using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaTags.Services;

/// <summary>
/// Service for converting between ISO language codes and language names.
/// </summary>
public class MediaConversionService
{
    private readonly ILogger<MediaConversionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaConversionService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the logger.</param>
    public MediaConversionService(ILogger<MediaConversionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts a list of ISO codes to their corresponding language names.
    /// </summary>
    /// <param name="isoCodes">List of ISO language codes.</param>
    /// <returns>List of language names.</returns>
    public List<string> ConvertIsoToLanguageNames(List<string> isoCodes)
    {
        return isoCodes.Select(ConvertSingleIsoToLanguageName).ToList();
    }

    /// <summary>
    /// Converts a list of language names to their corresponding ISO codes.
    /// </summary>
    /// <param name="languageNames">List of language names.</param>
    /// <returns>List of ISO codes.</returns>
    public List<string> ConvertLanguageNamesToIso(List<string> languageNames)
    {
        return languageNames.Select(ConvertSingleLanguageNameToIso).ToList();
    }

    /// <summary>
    /// Converts a single ISO code to its corresponding language name.
    /// </summary>
    /// <param name="isoCode">The ISO language code.</param>
    /// <returns>The language name or the original code if not found.</returns>
    private string ConvertSingleIsoToLanguageName(string isoCode)
    {
        if (ResolutionData.TryGetLanguageInfo(isoCode, out var languageInfo) &&
            languageInfo != null && !string.IsNullOrWhiteSpace(languageInfo.Name))
        {
            return languageInfo.Name;
        }

        _logger.LogWarning("Could not find language name for ISO code '{IsoCode}', using code as fallback", isoCode);
        return isoCode;
    }

    /// <summary>
    /// Converts a single language name to its corresponding ISO code.
    /// </summary>
    /// <param name="languageName">The language name.</param>
    /// <returns>The ISO code or the original name if not found.</returns>
    private string ConvertSingleLanguageNameToIso(string languageName)
    {
        var foundLanguage = ResolutionData.LanguageDictionary.Values
            .FirstOrDefault(lang => lang.Name.Equals(languageName, StringComparison.OrdinalIgnoreCase));

        if (foundLanguage != null)
        {
            return foundLanguage.Name;
        }

        // If it's already an ISO code (3 letters), keep it
        if (languageName.Length == 3)
        {
            return languageName;
        }

        _logger.LogWarning("Could not find ISO code for language name '{LanguageName}', using name as fallback", languageName);
        return languageName;
    }
}
