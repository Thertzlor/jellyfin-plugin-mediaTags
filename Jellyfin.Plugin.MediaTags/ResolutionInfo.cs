namespace Jellyfin.Plugin.MediaTags;

/// <summary>
/// Represents information about a language with its various ISO codes.
/// </summary>
public class ResolutionInfo
{
    /// <summary>
    /// Gets or sets the ISO 639-2 language code (3 letters).
    /// </summary>
    public int? MaxHeight { get; set; }

    /// <summary>
    /// Gets or sets the ISO 639-1 language code (2 letters).
    /// </summary>
    public int? MaxWidth { get; set; }

    /// <summary>
    /// Gets or sets the English name of the language.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
