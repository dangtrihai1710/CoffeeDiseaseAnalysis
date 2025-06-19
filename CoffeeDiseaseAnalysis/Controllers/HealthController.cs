// File: CoffeeDiseaseAnalysis/Controllers/HealthController.cs - FIXED CS0119
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;
using System.Text.Json;

namespace CoffeeDiseaseAnalysis.Controllers
{
    /// <summary>
    /// Health check controller for monitoring API status
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous] // ✅ Cho phép truy cập không cần authentication
    public class HealthController : ControllerBase
    {
        private readonly HealthCheckService _healthCheckService;
        private readonly ILogger<HealthController> _logger;

        public HealthController(HealthCheckService healthCheckService, ILogger<HealthController> logger)
        {
            _healthCheckService = healthCheckService;
            _logger = logger;
        }

        /// <summary>
        /// Get overall health status
        /// </summary>
        /// <returns>Health status information</returns>
        [HttpGet]
        [Route("")]
        [Route("status")]
        public async Task<IActionResult> GetHealth()
        {
            try
            {
                var health = await _healthCheckService.CheckHealthAsync();

                var response = new
                {
                    status = health.Status.ToString(),
                    timestamp = DateTime.UtcNow,
                    duration = health.TotalDuration.TotalMilliseconds,
                    service = "Coffee Disease Analysis API",
                    version = "1.3.0",
                    checks = health.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString(),
                        description = entry.Value.Description,
                        duration = entry.Value.Duration.TotalMilliseconds,
                        tags = entry.Value.Tags,
                        data = entry.Value.Data?.Count > 0 ? entry.Value.Data : null
                    }).ToList()
                };

                var statusCode = health.Status switch
                {
                    HealthStatus.Healthy => 200,
                    HealthStatus.Degraded => 200,
                    HealthStatus.Unhealthy => 503,
                    _ => 500
                };

                return StatusCode(statusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health status");
                return StatusCode(500, new
                {
                    status = "Error",
                    timestamp = DateTime.UtcNow,
                    error = "Unable to check health status",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Get ready/liveness check for critical services
        /// </summary>
        /// <returns>Critical services health status</returns>
        [HttpGet("ready")]
        public async Task<IActionResult> GetReadiness()
        {
            try
            {
                var health = await _healthCheckService.CheckHealthAsync(check =>
                    check.Tags.Contains("critical"));

                var response = new
                {
                    status = health.Status.ToString(),
                    timestamp = DateTime.UtcNow,
                    duration = health.TotalDuration.TotalMilliseconds,
                    message = health.Status == HealthStatus.Healthy ?
                        "All critical services are healthy" :
                        "Some critical services are unhealthy",
                    criticalServices = health.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString(),
                        description = entry.Value.Description,
                        duration = entry.Value.Duration.TotalMilliseconds
                    }).ToList()
                };

                var statusCode = health.Status == HealthStatus.Healthy ? 200 : 503;
                return StatusCode(statusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking readiness");
                return StatusCode(503, new
                {
                    status = "Error",
                    timestamp = DateTime.UtcNow,
                    error = "Unable to check readiness",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Get detailed system information
        /// </summary>
        /// <returns>Detailed system status</returns>
        [HttpGet("detailed")]
        public async Task<IActionResult> GetDetailedHealth()
        {
            try
            {
                var health = await _healthCheckService.CheckHealthAsync();

                // System information
                var systemInfo = new
                {
                    machineName = Environment.MachineName,
                    osVersion = Environment.OSVersion.ToString(),
                    processorCount = Environment.ProcessorCount,
                    workingSet = Environment.WorkingSet / 1024 / 1024, // MB
                    gcMemory = GC.GetTotalMemory(false) / 1024 / 1024, // MB
                    uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                    currentDirectory = Directory.GetCurrentDirectory()
                };

                var response = new
                {
                    status = health.Status.ToString(),
                    timestamp = DateTime.UtcNow,
                    duration = health.TotalDuration.TotalMilliseconds,
                    service = new
                    {
                        name = "Coffee Disease Analysis API",
                        version = "1.3.0",
                        environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
                    },
                    system = systemInfo,
                    healthChecks = health.Entries.GroupBy(entry =>
                        entry.Value.Tags.FirstOrDefault() ?? "general")
                        .ToDictionary(
                            group => group.Key,
                            group => group.Select(entry => new
                            {
                                name = entry.Key,
                                status = entry.Value.Status.ToString(),
                                description = entry.Value.Description,
                                duration = entry.Value.Duration.TotalMilliseconds,
                                tags = entry.Value.Tags,
                                data = entry.Value.Data?.Count > 0 ? entry.Value.Data : null,
                                exception = entry.Value.Exception?.Message
                            }).ToList()
                        ),
                    summary = new
                    {
                        total = health.Entries.Count,
                        healthy = health.Entries.Count(e => e.Value.Status == HealthStatus.Healthy),
                        degraded = health.Entries.Count(e => e.Value.Status == HealthStatus.Degraded),
                        unhealthy = health.Entries.Count(e => e.Value.Status == HealthStatus.Unhealthy)
                    }
                };

                var statusCode = health.Status switch
                {
                    HealthStatus.Healthy => 200,
                    HealthStatus.Degraded => 200,
                    HealthStatus.Unhealthy => 503,
                    _ => 500
                };

                return StatusCode(statusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed health information");
                return StatusCode(500, new
                {
                    status = "Error",
                    timestamp = DateTime.UtcNow,
                    error = "Unable to get detailed health information",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Simple ping endpoint for basic connectivity check
        /// </summary>
        /// <returns>Simple pong response</returns>
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new
            {
                message = "pong",
                timestamp = DateTime.UtcNow,
                service = "Coffee Disease Analysis API",
                version = "1.3.0"
            });
        }

        /// <summary>
        /// Get AI model health status
        /// </summary>
        /// <returns>AI model status</returns>
        [HttpGet("ai-model")]
        public IActionResult GetAiModelHealth()
        {
            try
            {
                var modelsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "models");
                var modelFile = Path.Combine(modelsPath, "coffee_resnet50_model_final.h5");

                var modelInfo = new
                {
                    modelExists = System.IO.File.Exists(modelFile), // ✅ FIXED: Use System.IO.File instead of File
                    modelPath = modelFile,
                    modelSize = System.IO.File.Exists(modelFile) ?
                        new FileInfo(modelFile).Length / 1024 / 1024 : 0, // MB
                    lastModified = System.IO.File.Exists(modelFile) ?
                        System.IO.File.GetLastWriteTime(modelFile) : (DateTime?)null,
                    modelsDirectory = new
                    {
                        exists = Directory.Exists(modelsPath),
                        fileCount = Directory.Exists(modelsPath) ?
                            Directory.GetFiles(modelsPath).Length : 0,
                        totalSize = Directory.Exists(modelsPath) ?
                            Directory.GetFiles(modelsPath)
                                .Sum(f => new FileInfo(f).Length) / 1024 / 1024 : 0 // MB
                    }
                };

                var status = modelInfo.modelExists ? "healthy" : "unhealthy";
                var statusCode = modelInfo.modelExists ? 200 : 503;

                return StatusCode(statusCode, new
                {
                    status,
                    timestamp = DateTime.UtcNow,
                    message = modelInfo.modelExists ?
                        "AI model is available and ready" :
                        "AI model file not found",
                    model = modelInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking AI model health");
                return StatusCode(500, new
                {
                    status = "error",
                    timestamp = DateTime.UtcNow,
                    error = "Unable to check AI model status",
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Get database health with connection details
        /// </summary>
        /// <returns>Database connection status</returns>
        [HttpGet("database")]
        public async Task<IActionResult> GetDatabaseHealth()
        {
            try
            {
                var health = await _healthCheckService.CheckHealthAsync(check =>
                    check.Tags.Contains("database") || check.Tags.Contains("db"));

                var dbEntry = health.Entries.FirstOrDefault(e =>
                    e.Key.Contains("database", StringComparison.OrdinalIgnoreCase));

                if (dbEntry.Key == null)
                {
                    return StatusCode(503, new
                    {
                        status = "error",
                        timestamp = DateTime.UtcNow,
                        message = "Database health check not configured"
                    });
                }

                var response = new
                {
                    status = dbEntry.Value.Status.ToString(),
                    timestamp = DateTime.UtcNow,
                    duration = dbEntry.Value.Duration.TotalMilliseconds,
                    description = dbEntry.Value.Description,
                    data = dbEntry.Value.Data,
                    message = dbEntry.Value.Status == HealthStatus.Healthy ?
                        "Database connection is healthy" :
                        "Database connection issues detected"
                };

                var statusCode = dbEntry.Value.Status == HealthStatus.Healthy ? 200 : 503;
                return StatusCode(statusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking database health");
                return StatusCode(503, new
                {
                    status = "error",
                    timestamp = DateTime.UtcNow,
                    error = "Unable to check database health",
                    message = ex.Message
                });
            }
        }
    }
}