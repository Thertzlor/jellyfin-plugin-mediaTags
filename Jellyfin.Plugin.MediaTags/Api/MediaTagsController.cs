using System;
using System.Net.Mime;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaTags.Api;

/// <summary>
/// The language tags Api controller.
/// </summary>
[ApiController]
[Authorize]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class MediaTagsController : ControllerBase, IDisposable
{
    private readonly MediaTagsManager _mediaTagsManager;
    private readonly ILogger<MediaTagsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaTagsController"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{MediaTagsController}"/> interface.</param>
    /// <param name="mediaTagsManager">Instance of the <see cref="MediaTagsManager"/> class.</param>
    public MediaTagsController(
        ILogger<MediaTagsController> logger,
        MediaTagsManager mediaTagsManager)
    {
        _logger = logger;
        _mediaTagsManager = mediaTagsManager;
    }

    /// <summary>
    /// Starts a manual FULL refresh of language tags.
    /// </summary>
    /// <param name="type">The type of refresh to perform. Default is "everything".</param>
    /// <response code="204">Library scan and language tagging started successfully. </response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpPost("RefreshMediaTags")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RefreshMetadataRequest([FromQuery] string type = "everything")
    {
        _logger.LogInformation("Starting a manual refresh of media tags for {Type}", type);
        await _mediaTagsManager.ScanLibrary(true, type).ConfigureAwait(false);
        _logger.LogInformation("Completed refresh of media tags for {Type}", type);
        return NoContent();
    }

    /// <summary>
    /// Removes all language tags from all content in the library.
    /// </summary>
    /// <response code="204">Language tags removal started successfully. </response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpPost("RemoveAllMediaTags")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RemoveAllMediaTagsRequest()
    {
        _logger.LogInformation("Starting removal of all language tags from library");
        await _mediaTagsManager.RemoveAllResolutionTags().ConfigureAwait(false);
        _logger.LogInformation("Completed removal of all language tags from library");
        return NoContent();
    }

    /// <summary>
    /// Applies non-media tags to configured item types.
    /// </summary>
    /// <response code="204">Non-media tagging started successfully. </response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpPost("ApplyNonMediaTags")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ApplyNonMediaTagsRequest()
    {
        _logger.LogInformation("Starting non-media item tagging");
        await _mediaTagsManager.ProcessNonMediaItems().ConfigureAwait(false);
        _logger.LogInformation("Completed non-media item tagging");
        return NoContent();
    }

    /// <summary>
    /// Removes non-media tags from all configured item types.
    /// </summary>
    /// <response code="204">Non-media tags removal started successfully. </response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpPost("RemoveNonMediaTags")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RemoveNonMediaTagsRequest()
    {
        _logger.LogInformation("Starting removal of non-media tags");
        await _mediaTagsManager.RemoveNonMediaTags().ConfigureAwait(false);
        _logger.LogInformation("Completed removal of non-media tags");
        return NoContent();
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
