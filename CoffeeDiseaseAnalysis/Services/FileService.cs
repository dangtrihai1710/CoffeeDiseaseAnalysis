// ==========================================
// 4. Services/FileService.cs - MISSING SERVICE
// ==========================================
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Services
{
    public class FileService : IFileService
    {
        private readonly ILogger<FileService> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png" };

        public FileService(ILogger<FileService> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public async Task<string> SaveFileAsync(IFormFile file, string directory)
        {
            try
            {
                var uploadsPath = Path.Combine(_environment.WebRootPath, directory);
                Directory.CreateDirectory(uploadsPath);

                var fileName = $"{Guid.NewGuid()}{GetFileExtension(file.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);

                return $"/{directory}/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving file");
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
                return false;
            }
        }

        public async Task<byte[]> ReadFileAsync(string filePath)
        {
            var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
            return await File.ReadAllBytesAsync(fullPath);
        }

        public string GetFileExtension(string fileName)
        {
            return Path.GetExtension(fileName).ToLowerInvariant();
        }

        public bool IsValidImageFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            var extension = GetFileExtension(file.FileName);
            return _allowedExtensions.Contains(extension);
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var testPath = Path.Combine(_environment.WebRootPath, "uploads");
                Directory.CreateDirectory(testPath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}