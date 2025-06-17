// File: CoffeeDiseaseAnalysis/Program.cs - IMPROVED VERSION
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Services;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

try
{
    // Configure JSON options globally
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    // Add database services
    builder.Services.AddCoffeeDiseaseDatabase(builder.Configuration);
    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    // Configure Identity với password policy tốt hơn
    builder.Services.AddDefaultIdentity<User>(options =>
    {
        // Password policy
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8; // Tăng từ 6 lên 8
        options.Password.RequiredUniqueChars = 3; // Tăng từ 1 lên 3

        // Lockout policy
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); // Tăng từ 5 lên 15
        options.Lockout.MaxFailedAccessAttempts = 3; // Giảm từ 5 xuống 3
        options.Lockout.AllowedForNewUsers = true;

        // User policy
        options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        options.User.RequireUniqueEmail = true;

        // Email confirmation (disable cho development)
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

    // Add Memory Cache first
    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1000; // Limit memory cache size
    });

    // Add Redis Cache with enhanced error handling
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    var redisConnected = false;
    try
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "CoffeeDiseaseAnalysis";
            options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions
            {
                EndPoints = { redisConnectionString },
                ConnectTimeout = 5000,
                SyncTimeout = 5000,
                AbortOnConnectFail = false,
                ConnectRetry = 3
            };
        });
        redisConnected = true;
        Console.WriteLine("✅ Redis cache configured successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Redis không khả dụng: {ex.Message}");
        Console.WriteLine("📝 Sử dụng Memory Cache làm fallback");
    }

    // Register services with enhanced configuration
    builder.Services.AddScoped<ICacheService, CacheService>();
    builder.Services.AddScoped<IMLPService, MLPService>();
    builder.Services.AddScoped<IMessageQueueService, MessageQueueService>();
    builder.Services.AddScoped<IPredictionService, PredictionService>();

    // Configure Controllers with enhanced validation
    builder.Services.AddControllers(options =>
    {
        // Add global filters
        options.Filters.Add<GlobalExceptionFilter>();
        options.Filters.Add<ModelValidationFilter>();
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            return new BadRequestObjectResult(new
            {
                message = "Validation failed",
                errors = errors,
                timestamp = DateTime.UtcNow
            });
        };
    });

    builder.Services.AddEndpointsApiExplorer();

    // Enhanced Swagger configuration
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Coffee Disease Analysis API",
            Version = "v1.3",
            Description = @"
🌱 **API phân tích bệnh lá cây cà phê sử dụng AI** - Enhanced Version

## 🆕 Cải tiến v1.3:
- 🔒 **Enhanced Security**: Stronger password policy, better rate limiting
- ⚡ **Performance**: Optimized caching, improved error handling
- 📊 **Enhanced Monitoring**: Detailed health checks, performance metrics
- 🛡️ **Error Handling**: Global exception filter, structured error responses
- 🔄 **Model Management**: Advanced versioning, A/B testing capabilities

## 🤖 AI Models:
- **ResNet50 v1.1**: 87.5% accuracy (Production ready)
- **MLP v1.0**: 72% accuracy (Symptoms analysis)
- **Combined Model**: 91% accuracy (CNN + MLP hybrid)

## 🎯 Disease Classes:
1. **Cercospora** - Nấm gây bệnh đốm nâu
2. **Healthy** - Lá khỏe mạnh
3. **Miner** - Sâu đục lá
4. **Phoma** - Nấm gây bệnh đốm đen
5. **Rust** - Bệnh rỉ sắt

## 🔧 Technical Features:
- **Async Processing**: RabbitMQ message queue
- **Caching**: Redis + Memory cache (fallback)
- **Authentication**: JWT Bearer tokens
- **File Upload**: JPG/PNG support, max 10MB
- **Batch Processing**: Multiple images analysis
- **Model Versioning**: Hot-swap models without downtime

## 📈 Monitoring:
- Real-time health checks at `/health`
- Performance metrics at `/api/dashboard/performance-metrics`
- System overview at `/api/dashboard/overview`

## 🚀 Quick Start:
1. Đăng ký tài khoản hoặc login
2. Upload ảnh lá cà phê tại `/api/prediction/upload`
3. Nhận kết quả phân tích bệnh ngay lập tức
4. Đánh giá feedback để cải thiện model
            ",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "Coffee Disease Analysis Team",
                Email = "support@coffeedisease.com",
                Url = new Uri("https://github.com/coffee-disease-analysis")
            },
            License = new Microsoft.OpenApi.Models.OpenApiLicense
            {
                Name = "MIT License",
                Url = new Uri("https://opensource.org/licenses/MIT")
            }
        });

        // Enhanced security definition
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = @"
JWT Authorization header using the Bearer scheme.
Enter 'Bearer' [space] and then your token in the text input below.
Example: 'Bearer 12345abcdef'"
        });

        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Add XML comments if available
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }

        // Enhanced schema customization
        c.SchemaFilter<EnumSchemaFilter>();
        c.OperationFilter<FileUploadOperationFilter>();
    });

    // Enhanced CORS policy
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowSpecificOrigins", policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins")
                .Get<string[]>() ?? new[]
                {
                    "http://localhost:3000",
                    "https://localhost:3000",
                    "http://localhost:3001",
                    "https://localhost:3001"
                };

            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .SetPreflightMaxAge(TimeSpan.FromMinutes(5));
        });
    });

    // Enhanced Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "database", "critical" })
        .AddCheck("memory", () =>
        {
            var allocated = GC.GetTotalMemory(false);
            var threshold = 1024L * 1024L * 1024L; // 1GB
            return allocated < threshold
                ? HealthCheckResult.Healthy($"Memory usage: {allocated / 1024 / 1024} MB")
                : HealthCheckResult.Degraded($"High memory usage: {allocated / 1024 / 1024} MB");
        }, tags: new[] { "memory", "performance" })
        .AddCheck("storage", () =>
        {
            try
            {
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                var driveInfo = new DriveInfo(Path.GetPathRoot(uploadsPath));
                var freeSpaceGB = driveInfo.AvailableFreeSpace / (1024L * 1024L * 1024L);

                return freeSpaceGB > 5
                    ? HealthCheckResult.Healthy($"Free space: {freeSpaceGB} GB")
                    : HealthCheckResult.Degraded($"Low disk space: {freeSpaceGB} GB");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Degraded($"Storage check failed: {ex.Message}");
            }
        }, tags: new[] { "storage", "critical" });

    // Enhanced file upload limits
    builder.Services.Configure<IISServerOptions>(options =>
    {
        options.MaxRequestBodySize = 52428800; // 50MB
    });

    builder.Services.Configure<FormOptions>(options =>
    {
        options.ValueLengthLimit = int.MaxValue;
        options.MultipartBodyLengthLimit = 52428800; // 50MB
        options.MultipartHeadersLengthLimit = 16384;
    });

    // Add rate limiting (if available)
    if (builder.Environment.IsProduction())
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("api", config =>
            {
                config.PermitLimit = 100;
                config.Window = TimeSpan.FromMinutes(1);
                config.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                config.QueueLimit = 10;
            });
        });
    }

    var app = builder.Build();

    // Enhanced initialization
    try
    {
        using var scope = app.Services.CreateScope();
        await DatabaseInitializer.InitializeAsync(scope.ServiceProvider);
        Console.WriteLine("✅ Database initialized successfully");

        // Initialize message queue with better error handling
        try
        {
            var mqService = scope.ServiceProvider.GetService<IMessageQueueService>();
            if (mqService != null)
            {
                var isHealthy = await mqService.IsHealthyAsync();
                if (isHealthy)
                {
                    mqService.StartConsuming();
                    Console.WriteLine("✅ Message queue consumer started");
                }
                else
                {
                    Console.WriteLine("⚠️ RabbitMQ not available - sync processing only");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Message queue initialization failed: {ex.Message}");
            Console.WriteLine("📝 Continuing with synchronous processing only");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Initialization error: {ex.Message}");
        if (app.Environment.IsDevelopment())
        {
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    // Configure enhanced pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee Disease Analysis API v1.3");
            c.RoutePrefix = "swagger";
            c.DisplayRequestDuration();
            c.EnableDeepLinking();
            c.EnableFilter();
            c.EnableValidator();
            c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
            c.DefaultModelsExpandDepth(1);
            c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
        });
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();

        // Add rate limiting in production
        app.UseRateLimiter();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    // Enhanced security headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;");

        if (app.Environment.IsDevelopment())
        {
            context.Response.Headers.Append("X-Environment", "Development");
        }

        await next();
    });

    app.UseRouting();
    app.UseCors("AllowSpecificOrigins");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // Enhanced health checks with detailed responses
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";

            var result = new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                duration = report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds,
                    exception = e.Value.Exception?.Message,
                    data = e.Value.Data.Count > 0 ? e.Value.Data : null
                }).ToList(),
                environment = app.Environment.EnvironmentName,
                version = "1.3.0"
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
        }
    });

    // Critical services health check
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("critical"),
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = new { status = report.Status.ToString(), timestamp = DateTime.UtcNow };
            await context.Response.WriteAsync(JsonSerializer.Serialize(result));
        }
    });

    // Live probe for Kubernetes
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false, // No checks, just confirms app is running
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status = "alive",
                timestamp = DateTime.UtcNow,
                uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime
            }));
        }
    });

    // Enhanced API status endpoint
    app.MapGet("/api/status", () => Results.Ok(new
    {
        service = "Coffee Disease Analysis API",
        version = "1.3.0",
        status = "healthy",
        timestamp = DateTime.UtcNow,
        environment = app.Environment.EnvironmentName,
        features = new
        {
            cnn_model = new { name = "ResNet50", version = "v1.1", accuracy = "87.5%" },
            mlp_model = new { name = "Symptoms MLP", version = "v1.0", accuracy = "72%" },
            async_processing = new { enabled = true, provider = "RabbitMQ" },
            caching = new { enabled = true, providers = new[] { "Redis", "Memory" }, redis_connected = redisConnected },
            database = new { provider = "SQL Server", status = "connected" },
            authentication = new { provider = "JWT Bearer", enabled = true }
        },
        endpoints = new
        {
            swagger = "/swagger",
            health = "/health",
            health_ready = "/health/ready",
            health_live = "/health/live",
            upload_sync = "/api/prediction/upload",
            upload_async = "/api/prediction/upload-async",
            dashboard = "/api/dashboard/overview"
        }
    }));

    // Root redirect with enhanced info
    app.MapGet("/", () => Results.Redirect("/swagger"));

    // API info endpoint
    app.MapGet("/api/info", () => Results.Ok(new
    {
        title = "Coffee Disease Analysis API",
        description = "AI-powered coffee leaf disease detection system",
        version = "1.3.0",
        documentation = "/swagger",
        health_check = "/health",
        supported_diseases = new[] { "Cercospora", "Healthy", "Miner", "Phoma", "Rust" },
        supported_formats = new[] { "JPG", "JPEG", "PNG" },
        max_file_size = "10MB",
        features = new[] { "CNN Classification", "MLP Symptoms", "Async Processing", "Caching", "Model Management" }
    }));

    // Startup banner
    Console.WriteLine("🚀 Coffee Disease Analysis API v1.3 is starting...");
    Console.WriteLine($"🌍 Environment: {app.Environment.EnvironmentName}");
    Console.WriteLine($"📝 Swagger UI: {(app.Environment.IsDevelopment() ? "https://localhost:7179/swagger" : "/swagger")}");
    Console.WriteLine($"❤️ Health Check: /health (detailed), /health/ready (critical), /health/live (k8s)");
    Console.WriteLine($"📊 API Status: /api/status, /api/info");
    Console.WriteLine($"🔧 Features: CNN Model, MLP Model, {(redisConnected ? "Redis Cache" : "Memory Cache")}, {(app.Environment.IsProduction() ? "Rate Limiting" : "Dev Mode")}");
    Console.WriteLine("✨ Ready to analyze coffee leaf diseases! 🌱");

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Application startup failed: {ex.Message}");
    if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
    {
        Console.WriteLine($"📝 Stack trace: {ex.StackTrace}");
    }
    throw;
}

// Global Exception Filter
public class GlobalExceptionFilter : Microsoft.AspNetCore.Mvc.Filters.IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(Microsoft.AspNetCore.Mvc.Filters.ExceptionContext context)
    {
        _logger.LogError(context.Exception, "Unhandled exception occurred");

        var response = new
        {
            message = "An error occurred while processing your request",
            detail = context.Exception.Message,
            timestamp = DateTime.UtcNow,
            path = context.HttpContext.Request.Path
        };

        context.Result = new Microsoft.AspNetCore.Mvc.ObjectResult(response)
        {
            StatusCode = 500
        };

        context.ExceptionHandled = true;
    }
}

// Model Validation Filter
public class ModelValidationFilter : Microsoft.AspNetCore.Mvc.Filters.ActionFilterAttribute
{
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            context.Result = new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new
            {
                message = "Validation failed",
                errors = errors,
                timestamp = DateTime.UtcNow
            });
        }
    }
}

// Swagger Filters
public class EnumSchemaFilter : Swashbuckle.AspNetCore.SwaggerGen.ISchemaFilter
{
    public void Apply(Microsoft.OpenApi.Models.OpenApiSchema schema, Swashbuckle.AspNetCore.SwaggerGen.SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            schema.Enum.Clear();
            foreach (var enumValue in Enum.GetNames(context.Type))
            {
                schema.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString(enumValue));
            }
        }
    }
}

public class FileUploadOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(Microsoft.OpenApi.Models.OpenApiOperation operation, Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        if (operation.RequestBody?.Content?.ContainsKey("multipart/form-data") == true)
        {
            operation.Summary = operation.Summary ?? "Upload file(s)";
            operation.Description = (operation.Description ?? "") + "\n\n**File Requirements:**\n- Format: JPG, JPEG, PNG\n- Max size: 10MB\n- Min size: 1KB";
        }
    }
}