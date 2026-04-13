using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MediaBrowser.Controller.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaTags.Services;

/// <summary>
/// Service for extracting subtitle language information from external subtitle files.
/// </summary>
public class HdrExtractionService
{
    private readonly ILogger<HdrExtractionService> _logger;
    private static readonly Regex HdrTypesRegex = new(@"\.(HDR|Do?Vi?)\.", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the <see cref="HdrExtractionService"/> class.
    /// </summary>
    /// <param name="logger">Instance of the logger.</param>
    public HdrExtractionService(
        ILogger<HdrExtractionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts language codes from a subtitle filename.
    /// </summary>
    /// <param name="videoFile">The subtitle filename.</param>
    /// <returns>List of extracted language ISO codes.</returns>
    public IEnumerable<string> ExtractHdrTypesFromFilename(string videoFile)
    {
        return HdrTypesRegex.Matches(videoFile)
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value.ToLowerInvariant());
    }
}
