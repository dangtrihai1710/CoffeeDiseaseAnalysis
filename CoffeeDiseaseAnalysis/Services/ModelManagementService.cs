// File: CoffeeDiseaseAnalysis/Services/ModelManagementService.cs - FIXED CS0019
using CoffeeDiseaseAnalysis.Services.Interfaces;
using CoffeeDiseaseAnalysis.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeDiseaseAnalysis.Services
{
    public class ModelManagementService : IModelManagementService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ModelManagementService> _logger;

        public ModelManagementService(ApplicationDbContext context, ILogger<ModelManagementService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<object> GetActiveModelAsync()
        {
            try
            {
                var activeModel = await _context.ModelVersions
                    .Where(m => m.IsActive)
                    .FirstOrDefaultAsync();

                // FIXED CS0019: Handle null case properly without ?? operator
                if (activeModel == null)
                {
                    return new { Message = "No active model found" };
                }

                return new
                {
                    activeModel.Id,
                    activeModel.ModelName,
                    activeModel.Version,
                    activeModel.FilePath,
                    activeModel.Accuracy,
                    activeModel.ValidationAccuracy,
                    activeModel.TestAccuracy,
                    activeModel.IsActive,
                    activeModel.IsProduction,
                    activeModel.CreatedAt,
                    activeModel.DeployedAt,
                    activeModel.ModelType,
                    activeModel.Notes
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active model");
                return new { Error = "Failed to get active model", Message = ex.Message };
            }
        }

        public async Task<bool> SwitchModelAsync(int modelId)
        {
            try
            {
                var model = await _context.ModelVersions.FindAsync(modelId);
                if (model == null)
                {
                    _logger.LogWarning("Model with ID {ModelId} not found", modelId);
                    return false;
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Deactivate all models of the same type
                    var allModelsOfType = await _context.ModelVersions
                        .Where(m => m.ModelType == model.ModelType)
                        .ToListAsync();

                    foreach (var m in allModelsOfType)
                    {
                        m.IsActive = false;
                        m.IsProduction = false;
                    }

                    // Activate selected model
                    model.IsActive = true;
                    model.DeployedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Successfully switched to model {ModelName} v{Version}",
                        model.ModelName, model.Version);

                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error switching to model {ModelId}", modelId);
                return false;
            }
        }

        public async Task<object> GetModelPerformanceAsync(int modelId)
        {
            try
            {
                var model = await _context.ModelVersions.FindAsync(modelId);
                if (model == null)
                {
                    return new { Error = "Model not found", ModelId = modelId };
                }

                // Get prediction statistics for this model
                var totalPredictions = await _context.Predictions
                    .Where(p => p.ModelVersion == model.Version)
                    .CountAsync();

                var avgConfidence = await _context.Predictions
                    .Where(p => p.ModelVersion == model.Version)
                    .AverageAsync(p => (double?)p.Confidence) ?? 0.0;

                var avgProcessingTime = await _context.Predictions
                    .Where(p => p.ModelVersion == model.Version)
                    .AverageAsync(p => (double?)p.ProcessingTimeMs) ?? 0.0;

                // Get feedback statistics
                var feedbackStats = await _context.Feedbacks
                    .Include(f => f.Prediction)
                    .Where(f => f.Prediction.ModelVersion == model.Version)
                    .GroupBy(f => f.Rating)
                    .Select(g => new { Rating = g.Key, Count = g.Count() })
                    .ToListAsync();

                var avgRating = feedbackStats.Any()
                    ? feedbackStats.Sum(f => f.Rating * f.Count) / (double)feedbackStats.Sum(f => f.Count)
                    : 0.0;

                return new
                {
                    ModelInfo = new
                    {
                        model.Id,
                        model.ModelName,
                        model.Version,
                        model.ModelType,
                        model.Accuracy,
                        model.ValidationAccuracy,
                        model.TestAccuracy,
                        model.IsActive,
                        model.IsProduction,
                        model.CreatedAt,
                        model.DeployedAt
                    },
                    Performance = new
                    {
                        TotalPredictions = totalPredictions,
                        AverageConfidence = Math.Round(avgConfidence, 4),
                        AverageProcessingTimeMs = Math.Round(avgProcessingTime, 2),
                        AverageRating = Math.Round(avgRating, 2),
                        FeedbackDistribution = feedbackStats
                    },
                    TrainingMetrics = new
                    {
                        model.TrainingSamples,
                        model.ValidationSamples,
                        model.TestSamples,
                        model.TrainingDatasetVersion
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance for model {ModelId}", modelId);
                return new { Error = "Failed to get model performance", Message = ex.Message };
            }
        }

        public async Task<bool> ValidateModelAsync(string modelPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(modelPath))
                {
                    return false;
                }

                // Check if file exists
                if (!File.Exists(modelPath))
                {
                    _logger.LogWarning("Model file not found: {ModelPath}", modelPath);
                    return false;
                }

                // Check file extension
                var extension = Path.GetExtension(modelPath).ToLowerInvariant();
                var validExtensions = new[] { ".onnx", ".h5", ".pkl", ".model" };

                if (!validExtensions.Contains(extension))
                {
                    _logger.LogWarning("Invalid model file extension: {Extension}", extension);
                    return false;
                }

                // Check file size (should be reasonable for a model)
                var fileInfo = new FileInfo(modelPath);
                if (fileInfo.Length == 0)
                {
                    _logger.LogWarning("Model file is empty: {ModelPath}", modelPath);
                    return false;
                }

                if (fileInfo.Length > 1024 * 1024 * 1024) // 1GB limit
                {
                    _logger.LogWarning("Model file too large: {Size} bytes", fileInfo.Length);
                    return false;
                }

                _logger.LogInformation("Model validation successful: {ModelPath}", modelPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating model: {ModelPath}", modelPath);
                return false;
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                // Check database connectivity
                var modelCount = await _context.ModelVersions.CountAsync();

                // Check if there's at least one active model
                var hasActiveModel = await _context.ModelVersions
                    .AnyAsync(m => m.IsActive);

                // Check if model files exist
                var activeModels = await _context.ModelVersions
                    .Where(m => m.IsActive)
                    .ToListAsync();

                var allFilesExist = true;
                foreach (var model in activeModels)
                {
                    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                        model.FilePath.TrimStart('/'));

                    if (!File.Exists(fullPath))
                    {
                        allFilesExist = false;
                        _logger.LogWarning("Active model file missing: {FilePath}", fullPath);
                    }
                }

                var isHealthy = modelCount > 0 && hasActiveModel && allFilesExist;

                if (!isHealthy)
                {
                    _logger.LogWarning("Model management health check failed. Models: {Count}, Active: {HasActive}, Files: {FilesExist}",
                        modelCount, hasActiveModel, allFilesExist);
                }

                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Model management health check failed");
                return false;
            }
        }

        public async Task<object> GetModelVersionsAsync(int pageNumber = 1, int pageSize = 10, string? modelType = null)
        {
            try
            {
                var query = _context.ModelVersions.AsQueryable();

                if (!string.IsNullOrEmpty(modelType))
                {
                    query = query.Where(m => m.ModelType == modelType);
                }

                var totalCount = await query.CountAsync();

                var models = await query
                    .OrderByDescending(m => m.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(m => new
                    {
                        m.Id,
                        m.ModelName,
                        m.Version,
                        m.ModelType,
                        m.Accuracy,
                        m.ValidationAccuracy,
                        m.TestAccuracy,
                        m.IsActive,
                        m.IsProduction,
                        m.CreatedAt,
                        m.DeployedAt,
                        m.FileSizeBytes,
                        m.Notes
                    })
                    .ToListAsync();

                return new
                {
                    Success = true,
                    Data = models,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model versions");
                return new { Success = false, Error = "Failed to get model versions", Message = ex.Message };
            }
        }

        public async Task<bool> DeployModelToProductionAsync(int modelId)
        {
            try
            {
                var model = await _context.ModelVersions.FindAsync(modelId);
                if (model == null)
                {
                    return false;
                }

                if (!model.IsActive)
                {
                    _logger.LogWarning("Cannot deploy inactive model {ModelId} to production", modelId);
                    return false;
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Remove production flag from other models of same type
                    var productionModels = await _context.ModelVersions
                        .Where(m => m.ModelType == model.ModelType && m.IsProduction)
                        .ToListAsync();

                    foreach (var prodModel in productionModels)
                    {
                        prodModel.IsProduction = false;
                    }

                    // Set as production
                    model.IsProduction = true;
                    model.DeployedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Model {ModelName} v{Version} deployed to production",
                        model.ModelName, model.Version);

                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying model {ModelId} to production", modelId);
                return false;
            }
        }
    }
}