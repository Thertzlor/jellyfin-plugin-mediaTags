using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaTags.ScheduledTasks;

/// <summary>
/// Class representing a task to refresh library for new media tags.
/// </summary>
public class RefreshLibraryTask : IScheduledTask, IDisposable
{
    private readonly ILogger<RefreshLibraryTask> _logger;
    private readonly MediaTagsManager _mediaTagsManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshLibraryTask"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{RefreshLibraryTask}"/> interface.</param>
    /// <param name="mediaTagsManager">Instance of the <see cref="MediaTagsManager"/> class.</param>
    public RefreshLibraryTask(
        ILogger<RefreshLibraryTask> logger,
        MediaTagsManager mediaTagsManager)
    {
        _logger = logger;
        _mediaTagsManager = mediaTagsManager;
    }

    /// <inheritdoc/>
    public string Name => "Scan library for new media tags";

    /// <inheritdoc/>
    public string Key => "MediaTagsSetsRefreshLibraryTask";

    /// <inheritdoc/>
    public string Description => "Scans all items in the library for new media tags.";

    /// <inheritdoc/>
    public string Category => "Media Tags";

    /// <inheritdoc/>
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting MediaTags refresh library task");
        await _mediaTagsManager.ScanLibrary(false).ConfigureAwait(false);
        _logger.LogInformation("MediaTags refresh library task finished");
    }

    /// <inheritdoc/>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Run this task every 24 hours
        return [new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(24).Ticks }];
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool dispose)
    {
        if (dispose)
        {
            _mediaTagsManager.Dispose();
        }
    }
}