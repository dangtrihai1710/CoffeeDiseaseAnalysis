// File: CoffeeDiseaseAnalysis/Services/Mock/MockFileService.cs
using CoffeeDiseaseAnalysis.Services.Interfaces;

namespace CoffeeDiseaseAnalysis.Services.Mock
{
    public class MockFileService : IFileService
    {
        private readonly ILogger<MockFileService> _logger;

        public MockFileService(ILogger<MockFileService> logger)
        {
            _logger = logger;
        }

        public async Task<string> SaveFileAsync(IFormFile file, string directory)
        {
            await Task.Delay(100);
            var mockPath = $"/{directory}/{Guid.NewGuid()}-{file.FileName}";
            _logger.LogInformation("Mock: File saved to {Path}", mockPath);
            return mockPath;
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            await Task.Delay(50);
            _logger.LogInformation("Mock: File deleted at {Path}", filePath);
            return true;
        }

        public async Task<byte[]> ReadFileAsync(string filePath)
        {
            await Task.Delay(100);
            return "Mock file content"u8.ToArray();
        }

        public string GetFileExtension(string fileName)
        {
            return Path.GetExtension(fileName);
        }

        public bool IsValidImageFile(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension);
        }

        public async Task<bool> IsHealthyAsync()
        {
            await Task.Delay(25);
            return true;
        }
    }
}