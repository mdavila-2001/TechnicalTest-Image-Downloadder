using ImageDownloader.Models;

namespace ImageDownloader.Services
{
    public interface IImageDownloaderService
    {
        Task<ResponseDownload> DownloadImagesAsync(RequestDownload request);
        Task<string> GetImageBase64ByNameAsync(string imageName);
    }
}
