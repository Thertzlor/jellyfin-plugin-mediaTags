using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.MediaTags;

/// <summary>
/// Static class containing resolution data and utilities.
/// </summary>
public static class ResolutionData
{
    private static readonly List<ResolutionInfo> _resolutionList = InitializeResolutionList();

    /// <summary>
    /// Gets the resolution dictionary for fast lookups.
    /// </summary>
    public static List<ResolutionInfo> ResolutionList => _resolutionList;

    private static List<ResolutionInfo> InitializeResolutionList()
    {
        var resolutions = new List<ResolutionInfo>();

        void AddResolution(ResolutionInfo res)
        {
            resolutions.Add(res);
        }

        AddResolution(new ResolutionInfo { MaxHeight = 2160, MaxWidth = 3840, Name = "4K" });
        AddResolution(new ResolutionInfo { MaxHeight = 1080, MaxWidth = 1920, Name = "1080p" });
        AddResolution(new ResolutionInfo { MaxHeight = 720, MaxWidth = 1280, Name = "720p" });

        return resolutions;
    }
}
