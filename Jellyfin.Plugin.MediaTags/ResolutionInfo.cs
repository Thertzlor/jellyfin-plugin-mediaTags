namespace Jellyfin.Plugin.MediaTags;

/// <summary>
/// Represents information about a resolution with its various ISO codes.
/// </summary>
public class ResolutionInfo
{
    /// <summary>
    /// Gets or sets height boundary.
    /// </summary>
    public int? MaxHeight { get; set; }

    /// <summary>
    /// Gets or sets the width boundary.
    /// </summary>
    public int? MaxWidth { get; set; }

    /// <summary>
    /// Gets or sets the name of the resolution.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
