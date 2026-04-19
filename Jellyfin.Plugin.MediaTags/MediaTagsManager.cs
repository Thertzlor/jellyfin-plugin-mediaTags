using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Extensions;
using Jellyfin.Plugin.MediaTags.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaTags;

/// <summary>
/// Class MediaTagsManager.
/// </summary>
public sealed class MediaTagsManager : IHostedService, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<MediaTagsManager> _logger;
    private readonly ConfigurationService _configService;
    private readonly MediaTagService _tagService;
    private readonly LibraryQueryService _queryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaTagsManager"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{MediaTagsManager}"/> interface.</param>
    /// <param name="configService">Instance of the configuration service.</param>
    /// <param name="tagService">Instance of the media tag service.</param>
    /// <param name="queryService">Instance of the library query service.</param>
    public MediaTagsManager(
        ILibraryManager libraryManager,
        ILogger<MediaTagsManager> logger,
        ConfigurationService configService,
        MediaTagService tagService,
        LibraryQueryService queryService)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _configService = configService;
        _tagService = tagService;
        _queryService = queryService;
    }

    // ***********************************
    // *          API Methods            *
    // ***********************************

    /// <summary>
    /// Scans the library.
    /// </summary>
    /// <param name="fullScan">if set to <c>true</c> [full scan].</param>
    /// <param name="type">The type of refresh to perform. Default is "everything".</param>
    /// <returns>A <see cref="Task"/> representing the library scan progress.</returns>
    public async Task ScanLibrary(bool fullScan = false, string type = "everything")
    {
        // Get configuration values
        fullScan = fullScan || _configService.AlwaysForceFullRefresh;
        var synchronously = _configService.SynchronousRefresh;
        var hdrTags = _configService.AddHdrTags;
        var hdrPlusTags = _configService.AddHdrPlusTags;
        var dvTags = _configService.AddDVTags;
        var dvpTags = _configService.AddDVPTags;

        // Get prefixes and whitelist once at the start to avoid repeated queries
        var resolutionPrefix = _configService.GetResolutionPrefix();
        var hdrPrefix = _configService.GetHdrTypePrefix();
        var whitelist = MediaTagService.ParseWhitelist(_configService.WhitelistTags);
        var tagSeriesOnly = _configService.TagSeriesOnly;

        _logger.LogInformation(
            "Scan configuration - Resolution prefix: '{ResolutionPrefix}', HDR prefix: '{HdrPrefix}', Whitelist: {WhitelistCount} codes ({Whitelist}), Tag Series Only: {TagSeriesOnly}",
            resolutionPrefix,
            hdrPrefix,
            whitelist.Count,
            whitelist.Count > 0 ? string.Join(", ", whitelist) : "none",
            tagSeriesOnly);

        LogScanConfiguration(fullScan, synchronously, hdrTags, hdrPlusTags, dvTags, dvpTags, tagSeriesOnly);

        // Create scan context to pass parameters
        var scanContext = (resolutionPrefix, hdrPrefix, whitelist, tagSeriesOnly);

        // Process the libraries
        switch (type.ToLowerInvariant())
        {
            case "movies":
                await ProcessLibraryMovies(fullScan, synchronously, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext).ConfigureAwait(false);
                break;
            case "series":
            case "tvshows":
                await ProcessLibrarySeries(fullScan, synchronously, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext).ConfigureAwait(false);
                break;
            case "collections":
                await ProcessLibraryCollections(fullScan, synchronously, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext).ConfigureAwait(false);
                break;
            default:
                await ProcessAllLibraryTypes(fullScan, synchronously, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Removes all media tags from all content in the library.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the removal process.</returns>
    public async Task RemoveAllMediaTags()
    {
        _logger.LogInformation("Starting removal of all media tags from library");

        try
        {
            var itemTypesToRemove = new[]
            {
                (BaseItemKind.Movie, "movies"),
                (BaseItemKind.Episode, "episodes"),
                (BaseItemKind.Season, "seasons"),
                (BaseItemKind.Series, "series"),
                (BaseItemKind.BoxSet, "collections")
            };

            foreach (var (itemKind, itemTypeName) in itemTypesToRemove)
            {
                await RemoveMediaTagsFromItemType(itemKind, itemTypeName).ConfigureAwait(false);
            }

            _logger.LogInformation("Completed removal of all media tags from library");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing all media tags from library");
            throw;
        }
    }

    /// <summary>
    /// Removes non-media tags from all items.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the removal process.</returns>
    public async Task RemoveNonMediaTags()
    {
        var tagName = _configService.NonMediaTag;
        var itemTypes = GetConfiguredItemTypes(_configService.NonMediaItemTypes);

        _logger.LogInformation("Starting removal of non-media tag '{TagName}' from library", tagName);

        if (itemTypes.Count == 0)
        {
            _logger.LogWarning("No non-media item types configured for tag removal");
            return;
        }

        try
        {
            foreach (var itemType in itemTypes)
            {
                await RemoveNonMediaTagFromItemType(itemType, tagName).ConfigureAwait(false);
            }

            _logger.LogInformation("Completed removal of non-media tags");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing non-media tags from library");
            throw;
        }
    }

    // ***********************************
    // * Library Scanning and Processing *
    // ***********************************

    /// <summary>
    /// Processes all library types in sequence.
    /// </summary>
    private async Task ProcessAllLibraryTypes(bool fullScan, bool synchronously, bool hdrTags, bool hdrPlusTags, bool dvTags, bool dvpTags, (string ResolutionPrefix, string HdrPrefix, List<string> Whitelist, bool TagSeriesOnly) scanContext)
    {
        await ProcessLibraryMovies(fullScan, synchronously, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext).ConfigureAwait(false);
        await ProcessLibrarySeries(fullScan, synchronously, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext).ConfigureAwait(false);
        await ProcessLibraryCollections(fullScan, synchronously, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext).ConfigureAwait(false);
        await ProcessNonMediaItems().ConfigureAwait(false);
    }

    /// <summary>
    /// Processes the libraries movies.
    /// </summary>
    /// <param name="fullScan">if set to <c>true</c> [full scan].</param>
    /// <param name="synchronously">if set to <c>true</c> [synchronously].</param>
    /// <param name="hdrTags">if set to <c>true</c> [extract hdr tags].</param>
    /// <param name="hdrPlusTags">if set to <c>true</c> [extract hdr plus tags].</param>
    /// <param name="dvTags">if set to <c>true</c> [extract dv tags].</param>
    /// <param name="dvpTags">if set to <c>true</c> [extract dv profile tags].</param>
    /// <param name="scanContext">Scan context containing prefixes and whitelist.</param>
    /// <returns>A <see cref="Task"/> representing the library scan progress.</returns>
    private async Task ProcessLibraryMovies(bool fullScan, bool synchronously, bool hdrTags, bool hdrPlusTags, bool dvTags, bool dvpTags, (string ResolutionPrefix, string HdrPrefix, List<string> Whitelist, bool TagSeriesOnly) scanContext)
    {
        LogProcessingHeader("Processing movies...");

        var movies = _queryService.GetMoviesFromLibrary();
        var (moviesProcessed, moviesSkipped) = await ProcessItemsAsync(
            movies,
            async (movie, ct) => await ProcessMovie(movie, fullScan, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext, ct).ConfigureAwait(false),
            synchronously).ConfigureAwait(false);

        _logger.LogInformation(
            "MOVIES - processed {Processed} of {Total} ({Skipped} skipped)",
            moviesProcessed,
            movies.Count,
            moviesSkipped);
    }

    private async Task<bool> ProcessMovie(Movie movie, bool fullScan, bool hdrTags, bool hdrPlusTags, bool dvTags, bool dvpTags, (string ResolutionPrefix, string HdrPrefix, List<string> Whitelist, bool TagSeriesOnly) scanContext, CancellationToken cancellationToken)
    {
        if (movie is not Video video)
        {
            return false;
        }

        var (shouldProcess, _, _) = CheckAndPrepareVideoForProcessing(video, fullScan, hdrTags, hdrPlusTags, dvTags, dvpTags, false, scanContext);

        if (shouldProcess)
        {
            var (resolutions, hdrTypes) = await ProcessVideo(video, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext, cancellationToken).ConfigureAwait(false);

            if (resolutions.Count > 0 || hdrTypes.Count > 0)
            {
                _logger.LogInformation(
                    "MOVIE - {MovieName} - resolution: {Res} - range: {Hdr}",
                    movie.Name,
                    resolutions.Count > 0 ? string.Join(", ", resolutions) : "none",
                    hdrTypes.Count > 0 ? string.Join(", ", hdrTypes) : "none");
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Processes the libraries series.
    /// </summary>
    /// <param name="fullScan">if set to <c>true</c> [full scan].</param>
    /// <param name="synchronously">if set to <c>true</c> [synchronously].</param>
    /// <param name="hdrTags">if set to <c>true</c> [extract hdr tags].</param>
    /// <param name="hdrPlusTags">if set to <c>true</c> [extract hdr plus tags].</param>
    /// <param name="dvTags">if set to <c>true</c> [extract dv tags].</param>
    /// <param name="dvpTags">if set to <c>true</c> [extract dv profile tags].</param>
    /// <param name="scanContext">Scan context containing prefixes and whitelist.</param>
    /// <returns>A <see cref="Task"/> representing the library scan progress.</returns>
    private async Task ProcessLibrarySeries(bool fullScan, bool synchronously, bool hdrTags, bool hdrPlusTags, bool dvTags, bool dvpTags, (string ResolutionPrefix, string HdrPrefix, List<string> Whitelist, bool TagSeriesOnly) scanContext)
    {
        LogProcessingHeader("Processing series...");

        var seriesList = _queryService.GetSeriesFromLibrary();
        var (processedSeries, skippedSeries) = await ProcessItemsAsync(
            seriesList,
            async (seriesBaseItem, ct) =>
            {
                if (seriesBaseItem is Series series)
                {
                    await ProcessSeries(series, fullScan, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext, ct).ConfigureAwait(false);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Series is null!");
                    return false;
                }
            },
            synchronously).ConfigureAwait(false);

        _logger.LogInformation(
            "SERIES - processed {Processed} of {Total} ({Skipped} skipped)",
            processedSeries,
            seriesList.Count,
            skippedSeries);
    }

    private async Task ProcessSeries(Series series, bool fullScan, bool hdrTags, bool hdrPlusTags, bool dvTags, bool dvpTags, (string ResolutionPrefix, string HdrPrefix, List<string> Whitelist, bool TagSeriesOnly) scanContext, CancellationToken cancellationToken)
    {
        var seasons = _queryService.GetSeasonsFromSeries(series);
        if (seasons == null || seasons.Count == 0)
        {
            _logger.LogWarning("No seasons found in SERIES {SeriesName}", series.Name);
            return;
        }

        var seriesResolutionsName = new List<string>();
        var seriesHdrTypesName = new List<string>();

        // Process all seasons and aggregate tags.
        foreach (var season in seasons)
        {
            var (seasonResolutions, seasonHdrTypes) = await ProcessSeason(
                season, series, fullScan, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext, cancellationToken)
                .ConfigureAwait(false);

            seriesResolutionsName.AddRange(seasonResolutions);
            seriesHdrTypesName.AddRange(seasonHdrTypes);
        }

        // Add resolution tags to series
        if (seriesResolutionsName.Count > 0)
        {
            seriesResolutionsName = await Task.Run(
                () => _tagService.AddMediaTags(series, seriesResolutionsName, TagType.Resolution, scanContext.ResolutionPrefix, scanContext.HdrPrefix, scanContext.Whitelist),
                cancellationToken).ConfigureAwait(false);
        }

        // Add range tags to series if enabled
        if (seriesHdrTypesName.Count > 0 && hdrTags)
        {
            seriesHdrTypesName = await Task.Run(
                () => _tagService.AddMediaTags(series, seriesHdrTypesName, TagType.Hdr, scanContext.ResolutionPrefix, scanContext.HdrPrefix, new List<string>()),
                cancellationToken).ConfigureAwait(false);
        }

        // Log series-level summary
        if (seriesResolutionsName.Count > 0 || seriesHdrTypesName.Count > 0)
        {
            _logger.LogInformation(
                "SERIES - {SeriesName} - resolution: {Resolution} - range: {Hdr}",
                series.Name,
                seriesResolutionsName.Count > 0 ? string.Join(", ", seriesResolutionsName) : "none",
                seriesHdrTypesName.Count > 0 ? string.Join(", ", seriesHdrTypesName) : "none");
        }

        // Save series to repository
        if (seriesResolutionsName.Count > 0 || (seriesHdrTypesName.Count > 0 && hdrTags))
        {
            await series.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Processes all episodes in a season and applies aggregated tags.
    /// </summary>
    /// <param name="season">The season to process.</param>
    /// <param name="series">The parent series.</param>
    /// <param name="fullScan">Whether this is a full scan.</param>
    /// <param name="hdrTags">Whether hdr processing is enabled.</param>
    /// <param name="hdrPlusTags">if set to <c>true</c> [extract hdr plus tags].</param>
    /// <param name="dvTags">if set to <c>true</c> [extract dv tags].</param>
    /// <param name="dvpTags">if set to <c>true</c> [extract dv profile tags].</param>
    /// <param name="scanContext">Scan context containing prefixes and whitelist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (resolutions, hdr tags).</returns>
    private async Task<(List<string> Resolutions, List<string> HdrTypes)> ProcessSeason(
          Season season,
          Series series,
          bool fullScan,
          bool hdrTags,
          bool hdrPlusTags,
          bool dvTags,
          bool dvpTags,
          (string ResolutionPrefix, string HdrPrefix, List<string> Whitelist, bool TagSeriesOnly) scanContext,
          CancellationToken cancellationToken)
    {
        var episodes = _queryService.GetEpisodesFromSeason(season);

        if (episodes == null || episodes.Count == 0)
        {
            _logger.LogWarning(
                "No episodes found in SEASON {SeasonName} of {SeriesName}",
                season.Name,
                series.Name);
            return (new List<string>(), new List<string>());
        }

        _logger.LogDebug("Processing SEASON {SeasonName} of {SeriesName}", season.Name, series.Name);

        var seasonResolutionsName = new List<string>();
        var seasonHdrTypesName = new List<string>();
        int episodesProcessed = 0;
        int episodesSkipped = 0;

        // Process each episode
        foreach (var episode in episodes)
        {
            var (resolutionNames, hdrNames, wasProcessed) =
                await ProcessEpisode(episode, fullScan, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext, cancellationToken)
                    .ConfigureAwait(false);

            seasonResolutionsName.AddRange(resolutionNames);
            seasonHdrTypesName.AddRange(hdrNames);

            if (wasProcessed)
            {
                episodesProcessed++;
            }
            else
            {
                episodesSkipped++;
            }
        }

        if (!scanContext.TagSeriesOnly)
        {
            // Add reoslution tags to season
            if (seasonResolutionsName.Count > 0)
            {
                seasonResolutionsName = await Task.Run(
                    () => _tagService.AddMediaTags(season, seasonResolutionsName, TagType.Resolution, scanContext.ResolutionPrefix, scanContext.HdrPrefix, scanContext.Whitelist),
                    cancellationToken).ConfigureAwait(false);
            }

            // Add range tags to season if enabled
            if (seasonHdrTypesName.Count > 0 && hdrTags)
            {
                seasonHdrTypesName = await Task.Run(
                    () => _tagService.AddMediaTags(season, seasonHdrTypesName, TagType.Hdr, scanContext.ResolutionPrefix, scanContext.HdrPrefix, new List<string>()),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            // Tag Series only: deduplicate aggregated names before returning to the series
            seasonResolutionsName = seasonResolutionsName.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            seasonHdrTypesName = seasonHdrTypesName.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Log season-level summary
        if (episodesProcessed > 0 && (seasonResolutionsName.Count > 0 || seasonHdrTypesName.Count > 0))
        {
            _logger.LogInformation(
                "  SEASON - {SeriesName} - {SeasonName} - processed {Processed} episodes of {Total} ({Skipped} skipped) - resolution: {Resolution} - range: {Hdr}",
                series.Name,
                season.Name,
                episodesProcessed,
                episodes.Count,
                episodesSkipped,
                seasonResolutionsName.Count > 0 ? string.Join(", ", seasonResolutionsName) : "none",
                seasonHdrTypesName.Count > 0 ? string.Join(", ", seasonHdrTypesName) : "none");
        }

        // Save season to repository only when it was tagged
        if (!scanContext.TagSeriesOnly && (seasonResolutionsName.Count > 0 || (seasonHdrTypesName.Count > 0 && hdrTags)))
        {
            await season.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken)
                .ConfigureAwait(false);
        }

        return (seasonResolutionsName, seasonHdrTypesName);
    }

    /// <summary>
    /// Processes a single episode, returning tags and whether it was processed.
    /// </summary>
    /// <param name="episode">The episode to process.</param>
    /// <param name="fullScan">Whether this is a full scan.</param>
    /// <param name="hdrTags">Whether hdr processing is enabled.</param>
    /// <param name="hdrPlusTags">if set to <c>true</c> [extract hdr plus tags].</param>
    /// <param name="dvTags">if set to <c>true</c> [extract dv tags].</param>
    /// <param name="dvpTags">if set to <c>true</c> [extract dv profile tags].</param>
    /// <param name="scanContext">Scan context containing prefixes and whitelist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (resolutions (Name), hdr tags (Name), was processed).</returns>
    private async Task<(List<string> Resolutions, List<string> HdrTypes, bool WasProcessed)> ProcessEpisode(
          Episode episode,
          bool fullScan,
          bool hdrTags,
          bool hdrPlusTags,
          bool dvTags,
          bool dvpTags,
          (string ResolutionPrefix, string HdrPrefix, List<string> Whitelist, bool TagSeriesOnly) scanContext,
          CancellationToken cancellationToken)
    {
        if (episode is not Video video)
        {
            return (new List<string>(), new List<string>(), false);
        }

        if (scanContext.TagSeriesOnly)
        {
            // Extract media data without applying tags or saving the episode.
            // The data still propagates up so seasons and series can be tagged correctly.
            var (resolutionNames, hdrNames) = await ProcessVideo(video, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext, cancellationToken, saveTags: false).ConfigureAwait(false);
            return (resolutionNames, hdrNames, true);
        }

        var (shouldProcess, existingResolutionsName, existingHdrTypesName) =
            CheckAndPrepareVideoForProcessing(video, fullScan, hdrTags, hdrPlusTags, dvTags, dvpTags, true, scanContext);

        if (shouldProcess)
        {
            var (newResolutionsName, newHdrTypesName) =
                await ProcessVideo(video, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext, cancellationToken).ConfigureAwait(false);

            // Save episode to repository
            await episode.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken)
                .ConfigureAwait(false);

            return (newResolutionsName, newHdrTypesName, true);
        }

        return (existingResolutionsName, existingHdrTypesName, false);
    }

    /// <summary>
    /// Processes the libraries collections.
    /// </summary>
    /// <param name="fullScan">if set to <c>true</c> [full scan].</param>
    /// <param name="synchronously">if set to <c>true</c> [synchronously].</param>
    /// <param name="hdrTags">if set to <c>true</c> [extract hdr tags].</param>
    /// <param name="hdrPlusTags">if set to <c>true</c> [extract hdr plus tags].</param>
    /// <param name="dvTags">if set to <c>true</c> [extract dv tags].</param>
    /// <param name="dvpTags">if set to <c>true</c> [extract dv profile tags].</param>
    /// <param name="scanContext">Scan context containing prefixes and whitelist.</param>
    /// <returns>A <see cref="Task"/> representing the library scan progress.</returns>
    private async Task ProcessLibraryCollections(bool fullScan, bool synchronously, bool hdrTags, bool hdrPlusTags, bool dvTags, bool dvpTags, (string ResolutionPrefix, string HdrPrefix, List<string> Whitelist, bool TagSeriesOnly) scanContext)
    {
        LogProcessingHeader("Processing collections...");

        var collections = _queryService.GetBoxSetsFromLibrary();
        var (collectionsProcessed, collectionsSkipped) = await ProcessItemsAsync(
            collections,
            async (collection, ct) => await ProcessCollection(collection, fullScan, hdrTags, hdrPlusTags, dvTags, dvpTags, scanContext, ct).ConfigureAwait(false),
            synchronously).ConfigureAwait(false);

        _logger.LogInformation(
            "COLLECTIONS - processed {Processed} of {Total} ({Skipped} skipped)",
            collectionsProcessed,
            collections.Count,
            collectionsSkipped);
    }

    private async Task<bool> ProcessCollection(BoxSet collection, bool fullRefresh, bool hdrTags, bool hdrPlusTags, bool dvTags, bool dvpTags, (string ResolutionPrefix, string HdrPrefix, List<string> Whitelist, bool TagSeriesOnly) scanContext, CancellationToken cancellationToken)
    {
        // Alternative approach using GetLinkedChildren if the above doesn't work:
        var collectionItems = collection.GetLinkedChildren()
            .OfType<Movie>()
            .ToList();

        if (collectionItems.Count == 0)
        {
            _logger.LogWarning("No movies found in box set {BoxSetName}", collection.Name);
            return false;
        }

        // Get media tags from all movies in the box set
        var collectionResolutions = new List<string>();
        var collectionHdrTypes = new List<string>();
        foreach (var movie in collectionItems)
        {
            if (movie == null)
            {
                _logger.LogWarning("Movie is null!");
                continue;
            }

            var movieResolutions = _tagService.GetResolutionTags(movie, TagType.Resolution, scanContext.ResolutionPrefix, scanContext.HdrPrefix);
            collectionResolutions.AddRange(movieResolutions);

            var movieHdrTypes = _tagService.GetResolutionTags(movie, TagType.Hdr, scanContext.ResolutionPrefix, scanContext.HdrPrefix);
            collectionHdrTypes.AddRange(movieHdrTypes);
        }

        // Strip resolution prefix
        collectionResolutions = _tagService.StripTagPrefix(collectionResolutions, TagType.Resolution, scanContext.ResolutionPrefix, scanContext.HdrPrefix);

        // Add media tags to the box set
        var addedResolutions = await Task.Run(() => _tagService.AddMediaTags(collection, collectionResolutions, TagType.Resolution, scanContext.ResolutionPrefix, scanContext.HdrPrefix, scanContext.Whitelist), cancellationToken).ConfigureAwait(false);

        // Strip range prefix
        collectionHdrTypes = _tagService.StripTagPrefix(collectionHdrTypes, TagType.Hdr, scanContext.ResolutionPrefix, scanContext.HdrPrefix);

        // Add range media tags to the box set
        List<string> addedHdrTypes = new List<string>();
        if (hdrTags && collectionHdrTypes.Count > 0)
        {
            addedHdrTypes = await Task.Run(() => _tagService.AddMediaTags(collection, collectionHdrTypes, TagType.Hdr, scanContext.ResolutionPrefix, scanContext.HdrPrefix, new List<string>()), cancellationToken).ConfigureAwait(false);
        }

        // Save collection to repository only once after all tag modifications
        // Only log if new tags were actually added
        if (addedResolutions.Count > 0 || addedHdrTypes.Count > 0)
        {
            await collection.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "COLLECTION - {CollectionName} - resolution: {Resolution} - range: {Hdr}",
                collection.Name,
                addedResolutions.Count > 0 ? string.Join(", ", addedResolutions) : "none",
                addedHdrTypes.Count > 0 ? string.Join(", ", addedHdrTypes) : "none");
            return true;
        }

        return false;
    }

    // ***********************************
    // Video Processing Helpers
    // ***********************************

    /// <summary>
    /// Common method to handle tag checking, removal and processing decision for video items.
    /// </summary>
    /// <param name="video">The video item to check.</param>
    /// <param name="fullScan">Whether this is a full scan.</param>
    /// <param name="hdrTags">Whether hdr processing is enabled.</param>
    /// <param name="hdrPlusTags">if set to <c>true</c> [extract hdr plus tags].</param>
    /// <param name="dvTags">if set to <c>true</c> [extract dv tags].</param>
    /// <param name="dvpTags">if set to <c>true</c> [extract dv profile tags].</param>
    /// <param name="getExistingTags">Whether to get existing tags or not.</param>
    /// <param name="scanContext">Scan context with prefixes and configuration.</param>
    /// <returns>Tuple indicating if video should be processed and any existing tags found.</returns>
    private (bool ShouldProcess, List<string> ExistingResolution, List<string> ExistingHDr) CheckAndPrepareVideoForProcessing(
          Video video, bool fullScan, bool hdrTags, bool hdrPlusTags, bool dvTags, bool dvpTags, bool getExistingTags, (string ResolutionPrefix, string HdrPrefix, List<string> Whitelist, bool TagSeriesOnly) scanContext)
    {
        bool shouldProcess = fullScan;
        var existingResolutionsName = new List<string>();
        var existingHdrTypesName = new List<string>();

        if (fullScan)
        {
            _tagService.RemoveMediaTags(video, TagType.Resolution, scanContext.ResolutionPrefix, scanContext.HdrPrefix);
            if (hdrTags)
            {
                _tagService.RemoveMediaTags(video, TagType.Hdr, scanContext.ResolutionPrefix, scanContext.HdrPrefix);
            }

            shouldProcess = true;
            return (shouldProcess, existingResolutionsName, existingHdrTypesName);
        }

        // Check resolution tags
        var hasResolutionTags = _tagService.HasResolutionTags(video, TagType.Resolution, scanContext.ResolutionPrefix, scanContext.HdrPrefix);
        if (hasResolutionTags)
        {
            if (getExistingTags)
            {
                existingResolutionsName = _tagService.StripTagPrefix(_tagService.GetResolutionTags(video, TagType.Resolution, scanContext.ResolutionPrefix, scanContext.HdrPrefix), TagType.Resolution, scanContext.ResolutionPrefix, scanContext.HdrPrefix);
            }
        }
        else
        {
            shouldProcess = true;
        }

        // Check range tags
        if (hdrTags)
        {
            var hasHdrTags = _tagService.HasResolutionTags(video, TagType.Hdr, scanContext.ResolutionPrefix, scanContext.HdrPrefix);
            if (hasHdrTags)
            {
                if (getExistingTags)
                {
                    existingHdrTypesName = _tagService.StripTagPrefix(_tagService.GetResolutionTags(video, TagType.Hdr, scanContext.ResolutionPrefix, scanContext.HdrPrefix), TagType.Hdr, scanContext.ResolutionPrefix, scanContext.HdrPrefix);
                }
            }
            else
            {
                shouldProcess = true;
            }
        }

        return (shouldProcess, existingResolutionsName, existingHdrTypesName);
    }

    private async Task<(List<string> Resolutions, List<string> HdrTypes)> ProcessVideo(Video video, bool hdrTags, bool hdrPlusTags, bool dvTags, bool dvpTags, (string ResolutionPrefix, string HdrPrefix, List<string> Whitelist, bool TagSeriesOnly) scanContext, CancellationToken cancellationToken, bool saveTags = true)
    {
        var resolutionsName = new List<string>();
        var hdrTypesName = new List<string>();

        try
        {
            // Get media sources from the video
            var mediaSources = video.GetMediaSources(false);

            if (mediaSources == null || mediaSources.Count == 0)
            {
                _logger.LogWarning("No media sources found for VIDEO {VideoName}", video.Name);

                if (saveTags)
                {
                    // Still try to add undefined tag if no sources found
                    await _tagService.AddMediaTagsOrUndefined(video, resolutionsName, scanContext.ResolutionPrefix, scanContext.HdrPrefix, scanContext.Whitelist, cancellationToken).ConfigureAwait(false);
                }

                return (resolutionsName, hdrTypesName);
            }

            foreach (var source in mediaSources)
            {
                if (source.MediaStreams == null || source.MediaStreams.Count == 0)
                {
                    continue;
                }

                // Extract video streams
                var videoStreams = source.MediaStreams
                    .Where(s => s.Type == MediaBrowser.Model.Entities.MediaStreamType.Video)
                    .ToList();

                foreach (var stream in videoStreams)
                {
                    var yRes = stream.Height;
                    var xRes = stream.Width;
                    resolutionsName.Add(ResolutionData.ResolutionList.Find(r => yRes >= r.MaxHeight || xRes >= r.MaxWidth)?.Name ?? "SD");
                    if (hdrTags)
                    {
                        if (stream.VideoRange == VideoRange.HDR)
                        {
                            if (dvTags && (stream.DvVersionMajor != null))
                            {
                                hdrTypesName.Add(dvpTags ? "DV" + (stream.DvProfile ?? 0) : "DV");
                            }
                            else
                            {
                                hdrTypesName.Add(hdrPlusTags && (stream.Hdr10PlusPresentFlag ?? false) ? "HDR10plus" : "HDR");
                            }
                        }
                        else
                        {
                            hdrTypesName.Add("SDR");
                        }
                    }
                }
            }

            if (saveTags)
            {
                // Add extracted resolutions if found
                if (resolutionsName.Count > 0)
                {
                    // Add media tags
                    resolutionsName = await Task.Run(() => _tagService.AddMediaTags(video, resolutionsName, TagType.Resolution, scanContext.ResolutionPrefix, scanContext.HdrPrefix, scanContext.Whitelist), cancellationToken).ConfigureAwait(false);
                    _logger.LogDebug("Added resolution tags for VIDEO {VideoName}: {Resolutions}", video.Name, string.Join(", ", resolutionsName));
                }
                else
                {
                    await _tagService.AddMediaTagsOrUndefined(video, resolutionsName, scanContext.ResolutionPrefix, scanContext.HdrPrefix, scanContext.Whitelist, cancellationToken).ConfigureAwait(false);
                }

                if (hdrTags && hdrTypesName.Count > 0)
                {
                    // TODO: HDR tags!
                    hdrTypesName = await Task.Run(() => _tagService.AddMediaTags(video, hdrTypesName, TagType.Hdr, scanContext.ResolutionPrefix, scanContext.HdrPrefix, new List<string>()), cancellationToken).ConfigureAwait(false);
                    _logger.LogDebug("Added ranhe tags for VIDEO {VideoName}: {HdrTypes}", video.Name, string.Join(", ", hdrTypesName));
                }
                else if (hdrTags)
                {
                    _logger.LogWarning("No range information found for VIDEO {VideoName}", video.Name);
                }

                // Save video to repository only once after all tag modifications
                await video.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
            }
            else // Read-only mode: convert ISO codes to names without modifying the item itself
            {
                // Read-only mode: convert ISO codes to names without modifying the item
                if (resolutionsName.Count > 0)
                {
                    var filtered = _tagService.FilterOutTags(video, resolutionsName, scanContext.Whitelist);
                    resolutionsName = filtered.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }

                if (hdrTags && hdrTypesName.Count > 0)
                {
                    var filtered = _tagService.FilterOutTags(video, hdrTypesName, scanContext.Whitelist);
                    hdrTypesName = filtered.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {VideoName}", video.Name);
        }

        return (resolutionsName, hdrTypesName);
    }

    /// <summary>
    /// Generic helper method to process items either asynchronously (parallel) or synchronously.
    /// </summary>
    /// <typeparam name="T">The type of items to process.</typeparam>
    /// <param name="items">List of items to process.</param>
    /// <param name="processor">Function to process each item, returns true if processed, false if skipped.</param>
    /// <param name="synchronously">If true, process items synchronously; if false, process in parallel.</param>
    /// <returns>Tuple of (processed count, skipped count).</returns>
    private async Task<(int Processed, int Skipped)> ProcessItemsAsync<T>(
        List<T> items,
        Func<T, CancellationToken, Task<bool>> processor,
        bool synchronously)
    {
        int processed = 0;
        int skipped = 0;

        if (!synchronously)
        {
            await Parallel.ForEachAsync(items, async (item, ct) =>
            {
                var wasProcessed = await processor(item, ct).ConfigureAwait(false);
                if (wasProcessed)
                {
                    Interlocked.Increment(ref processed);
                }
                else
                {
                    Interlocked.Increment(ref skipped);
                }
            }).ConfigureAwait(false);
        }
        else
        {
            foreach (var item in items)
            {
                var wasProcessed = await processor(item, CancellationToken.None).ConfigureAwait(false);
                if (wasProcessed)
                {
                    processed++;
                }
                else
                {
                    skipped++;
                }
            }
        }

        return (processed, skipped);
    }

    // ***********************************
    // *     Non-Media Item Tagging      *
    // ***********************************

    /// <summary>
    /// Removes media tags from items of a specific type.
    /// </summary>
    /// <param name="itemKind">The kind of item to remove tags from.</param>
    /// <param name="itemTypeName">The name of the item type for logging.</param>
    /// <returns>A <see cref="Task"/> representing the removal process.</returns>
    private async Task RemoveMediaTagsFromItemType(BaseItemKind itemKind, string itemTypeName)
    {
        var items = _libraryManager.QueryItems(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { itemKind },
            Recursive = true
        }).Items;

        _logger.LogInformation("Removing media tags from {Count} {Type}", items.Count, itemTypeName);

        var resolutionPrefix = _configService.GetResolutionPrefix();
        var hdrPrefix = _configService.GetHdrTypePrefix();

        foreach (var item in items)
        {
            _tagService.RemoveMediaTags(item, TagType.Resolution, resolutionPrefix, hdrPrefix);
            _tagService.RemoveMediaTags(item, TagType.Hdr, resolutionPrefix, hdrPrefix);
            await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Processes non-media items and applies tags.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the processing.</returns>
    public async Task ProcessNonMediaItems()
    {
        if (!_configService.EnableNonMediaTagging)
        {
            _logger.LogInformation("Non-media tagging is disabled");
            return;
        }

        var tagName = _configService.NonMediaTag;
        var itemTypes = GetConfiguredItemTypes(_configService.NonMediaItemTypes);

        if (itemTypes.Count == 0)
        {
            _logger.LogInformation("No non-media item types selected for tagging");
            return;
        }

        _logger.LogInformation("Applying tag '{TagName}' to {Count} item types", tagName, itemTypes.Count);
        LogProcessingHeader("Processing non-media items...");

        foreach (var itemType in itemTypes)
        {
            await ProcessNonMediaItemType(itemType, tagName).ConfigureAwait(false);
        }

        _logger.LogInformation("Completed non-media item tagging");
    }

    /// <summary>
    /// Processes a single non-media item type for tagging.
    /// </summary>
    private async Task ProcessNonMediaItemType(string itemType, string tagName)
    {
        try
        {
            if (!Enum.TryParse<BaseItemKind>(itemType, out var kind))
            {
                _logger.LogWarning("Unknown item type: {ItemType}", itemType);
                return;
            }

            var items = _libraryManager.QueryItems(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { kind },
                Recursive = true
            }).Items;

            _logger.LogInformation("Found {Count} {ItemType} items", items.Count, itemType);

            int taggedCount = 0;
            foreach (var item in items)
            {
                if (!item.Tags.Contains(tagName, StringComparer.OrdinalIgnoreCase))
                {
                    var tagsList = item.Tags.ToList();
                    tagsList.Add(tagName);
                    item.Tags = tagsList.ToArray();
                    await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None)
                        .ConfigureAwait(false);
                    taggedCount++;
                }
            }

            _logger.LogInformation("Tagged {TaggedCount} of {TotalCount} {ItemType} items", taggedCount, items.Count, itemType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing non-media items of type {ItemType}", itemType);
        }
    }

    /// <summary>
    /// Removes non-media tags from a specific item type.
    /// </summary>
    private async Task RemoveNonMediaTagFromItemType(string itemType, string tagName)
    {
        if (!Enum.TryParse<BaseItemKind>(itemType, out var kind))
        {
            return;
        }

        var items = _libraryManager.QueryItems(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { kind },
            Recursive = true
        }).Items;

        _logger.LogInformation("Removing tag from {Count} {ItemType} items", items.Count, itemType);

        int removedCount = 0;
        foreach (var item in items)
        {
            var originalCount = item.Tags.Length;
            item.Tags = item.Tags.Where(t =>
                !t.Equals(tagName, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (item.Tags.Length < originalCount)
            {
                await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None)
                    .ConfigureAwait(false);
                removedCount++;
            }
        }

        _logger.LogInformation("Removed tag from {RemovedCount} {ItemType} items", removedCount, itemType);
    }

    // ***************************
    // General Helpers
    // ***************************

    /// <summary>
    /// Logs the current scan configuration.
    /// </summary>
    private void LogScanConfiguration(bool fullScan, bool synchronously, bool hdrTags, bool hdrPlusTags, bool dvTags, bool dvpTags, bool tagSeriesOnly)
    {
        if (fullScan)
        {
            _logger.LogInformation("Full scan enabled");
        }

        if (synchronously)
        {
            _logger.LogInformation("Synchronous refresh enabled");
        }

        if (hdrTags)
        {
            _logger.LogInformation("Extract hdr tags enabled");
        }

        if (tagSeriesOnly)
        {
            _logger.LogInformation("TV show tagging: Tag Series only enabled");
        }
    }

    /// <summary>
    /// Gets configured item types from a comma-separated string.
    /// </summary>
    private static List<string> GetConfiguredItemTypes(string itemTypesString)
    {
        return itemTypesString.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    /// <summary>
    /// Logs a processing header with decorative borders.
    /// </summary>
    private void LogProcessingHeader(string message)
    {
        var border = new string('*', message.Length + 6);
        _logger.LogInformation("{Border}", border);
        _logger.LogInformation("*  {Message}   *", message);
        _logger.LogInformation("{Border}", border);
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
