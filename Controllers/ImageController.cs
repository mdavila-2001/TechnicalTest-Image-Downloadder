using ImageDownloader.Models;
using ImageDownloader.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageDownloader.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImageController : ControllerBase
    {
        private readonly IImageDownloaderService _imageService;
        private readonly ILogger<ImageController> _logger;

        public ImageController(IImageDownloaderService imageService, ILogger<ImageController> logger)
        {
            _imageService = imageService;
            _logger = logger;
        }

        [HttpPost("download")]
        public async Task<ActionResult<ResponseDownload>> DownloadImages([FromBody] RequestDownload request)
        {
            try
            {
                _logger.LogInformation($"Received download request for {request.ImageUrls?.Count() ?? 0} images, MaxDownloadAtOnce: {request.MaxDownloadAtOnce}");

                var response = await _imageService.DownloadImagesAsync(request);

                if (!response.Success)
                {
                    return BadRequest(response);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing download request");
                return StatusCode(500, new ResponseDownload
                {
                    Success = false,
                    Message = "Internal server error: " + ex.Message
                });
            }
        }

        [HttpGet("get-image-by-name/{imageName}")]
        public async Task<ActionResult> GetImageByName(string imageName)
        {
            try
            {
                _logger.LogInformation($"Requesting image: {imageName}");

                var base64String = await _imageService.GetImageBase64ByNameAsync(imageName);

                return Ok(new { base64String });
            }
            catch (FileNotFoundException)
            {
                return NotFound(new { message = $"Image '{imageName}' not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting image {imageName}");
                return StatusCode(500, new { message = "Internal server error: " + ex.Message });
            }
        }

    }
}
