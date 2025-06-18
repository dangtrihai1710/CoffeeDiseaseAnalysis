// ==========================================
// 3. Services/Interfaces/IFileService.cs - MISSING SERVICE
// ==========================================
namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IFileService
    {
        Task<string> SaveFileAsync(IFormFile file, string directory);
        Task<bool> DeleteFileAsync(string filePath);
        Task<byte[]> ReadFileAsync(string filePath);
        string GetFileExtension(string fileName);
        bool IsValidImageFile(IFormFile file);
        Task<bool> IsHealthyAsync();
    }
}