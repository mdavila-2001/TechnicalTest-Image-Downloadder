using ImageDownloader.Models;
using System.Collections.Concurrent;

namespace ImageDownloader.Services
{
    public class ImageDownloaderService:IImageDownloaderService
    {
        private readonly ILogger<ImageDownloaderService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _imagesPath;

        public ImageDownloaderService(ILogger<ImageDownloaderService> logger, HttpClient httpClient, IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;

            // Configurar el directorio para guardar las imágenes
            _imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "DownloadedImages");
            if (!Directory.Exists(_imagesPath))
            {
                Directory.CreateDirectory(_imagesPath);
            }
        }

        public async Task<ResponseDownload> DownloadImagesAsync(RequestDownload request)
        {
            var response = new ResponseDownload
            {
                Success = true,
                Message = "Images downloaded successfully",
                UrlAndNames = new Dictionary<string, string>()
            };

            if (request.ImageUrls == null || !request.ImageUrls.Any())
            {
                response.Success = false;
                response.Message = "No image URLs provided";
                return response;
            }

            try
            {
                // Crear una cola con todas las URLs
                var urls = new Queue<string>(request.ImageUrls);

                // Diccionario para almacenar resultados (thread-safe)
                var results = new ConcurrentDictionary<string, string>();

                // Semáforo para limitar las descargas simultáneas
                using var semaphore = new SemaphoreSlim(request.MaxDownloadAtOnce > 0 ? request.MaxDownloadAtOnce : 1);

                // Lista para mantener el seguimiento de todas las tareas
                var downloadTasks = new List<Task>();

                // Procesar todas las URLs en la cola
                while (urls.Count > 0)
                {
                    var url = urls.Dequeue();

                    // Esperar a que haya un slot disponible
                    await semaphore.WaitAsync();

                    // Crear y añadir la tarea de descarga
                    var downloadTask = Task.Run(async () =>
                    {
                        try
                        {
                            // Generar un nombre único para la imagen
                            string imageName = $"{Guid.NewGuid()}{Path.GetExtension(url) ?? ".jpg"}";
                            string imagePath = Path.Combine(_imagesPath, imageName);

                            // Descargar la imagen
                            var imageBytes = await _httpClient.GetByteArrayAsync(url);

                            // Guardar la imagen en el servidor
                            await File.WriteAllBytesAsync(imagePath, imageBytes);

                            // Añadir al diccionario de resultados
                            results.TryAdd(url, imageName);

                            _logger.LogInformation($"Downloaded image from {url} to {imagePath}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error downloading image from {url}");
                            results.TryAdd(url, $"Error: {ex.Message}");
                        }
                        finally
                        {
                            // Liberar el semáforo
                            semaphore.Release();
                        }
                    });

                    downloadTasks.Add(downloadTask);
                }

                // Esperar a que todas las tareas terminen
                await Task.WhenAll(downloadTasks);

                // Actualizar la respuesta con los resultados
                response.UrlAndNames = results;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in image download process");
                response.Success = false;
                response.Message = $"Error in download process: {ex.Message}";
                return response;
            }
        }

        public async Task<string> GetImageBase64ByNameAsync(string imageName)
        {
            if (string.IsNullOrEmpty(imageName))
            {
                throw new ArgumentException("Image name cannot be empty");
            }

            string imagePath = Path.Combine(_imagesPath, imageName);

            if (!File.Exists(imagePath))
            {
                _logger.LogWarning($"Image not found: {imagePath}");
                throw new FileNotFoundException($"Image {imageName} not found");
            }

            try
            {
                // Leer el archivo de imagen
                byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);

                // Convertir a Base64
                string base64String = Convert.ToBase64String(imageBytes);

                return base64String;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting base64 for image {imageName}");
                throw;
            }
        }
    }
}
