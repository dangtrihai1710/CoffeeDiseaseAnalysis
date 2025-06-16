// File: CoffeeDiseaseAnalysis/Program.cs - Updated Version
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Services;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add database services
builder.Services.AddCoffeeDiseaseDatabase(builder.Configuration);

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configure Identity
builder.Services.AddDefaultIdentity<User>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;

    // Sign in settings
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Add Redis Cache
try
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        options.InstanceName = "CoffeeDiseaseAnalysis";
    });

    Console.WriteLine("✅ Redis cache configured successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Redis không khả dụng: {ex.Message}");
    Console.WriteLine("📝 Sử dụng Memory Cache làm fallback");
}

// Add Memory Cache as fallback
builder.Services.AddMemoryCache();

// Add AI and Business Services
builder.Services.AddScoped<IPredictionService, PredictionService>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IMLPService, MLPService>();
builder.Services.AddSingleton<IMessageQueueService, MessageQueueService>();

// Add Controllers and API Explorer
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with enhanced settings
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Coffee Disease Analysis API",
        Version = "v1.1",
        Description = @"
🌱 **API phân tích bệnh lá cây cà phê sử dụng AI**

## Tính năng chính:
- 🤖 **CNN Model**: ResNet50 phân loại 5 loại bệnh (Cercospora, Healthy, Miner, Phoma, Rust)
- 🧠 **MLP Model**: Phân tích triệu chứng bổ sung
- ⚡ **Async Processing**: RabbitMQ cho xử lý bất đồng bộ
- 💾 **Caching**: Redis để tối ưu hiệu suất
- 📊 **Feedback System**: Học từ phản hồi người dùng

## Models hiện tại:
- **ResNet50 v1.1**: 87.5% accuracy (Production)
- **MLP v1.0**: 72% accuracy (Symptoms analysis)
- **Combined**: 91% accuracy (Kết hợp CNN + MLP)

## Các endpoint chính:
- `POST /api/prediction/upload` - Upload đồng bộ
- `POST /api/prediction/upload-async` - Upload bất đồng bộ  
- `POST /api/prediction/upload-batch` - Batch processing
- `GET /api/prediction/history` - Lịch sử dự đoán
- `POST /api/prediction/feedback` - Phản hồi cải thiện model
        ",
        Contact = new()
        {
            Name = "Coffee Disease Analysis Team",
            Email = "support@coffeedisease.com"
        }
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'"
    });

    c.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Organize endpoints by tags
    c.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] });
    c.DocInclusionPredicate((name, api) => true);
});

// Add CORS with enhanced configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000",
                "http://localhost:3001",
                "https://localhost:3001"
            )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("X-Pagination", "X-Total-Count");
    });
});

// Add Health Checks with comprehensive monitoring
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "database", "critical" })
    .AddEntityFrameworkCheck<ApplicationDbContext>("ef_database", tags: new[] { "database", "ef" })
    .AddCheck("ai_model", async () =>
    {
        try
        {
            var predictionService = builder.Services.BuildServiceProvider()
                .GetRequiredService<IPredictionService>();
            var isHealthy = await predictionService.HealthCheckAsync();
            return isHealthy ? HealthCheckResult.Healthy("AI model is responding")
                             : HealthCheckResult.Unhealthy("AI model is not responding");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("AI model check failed", ex);
        }
    }, tags: new[] { "ai", "critical" })
    .AddCheck("cache", async () =>
    {
        try
        {
            var cacheService = builder.Services.BuildServiceProvider()
                .GetRequiredService<ICacheService>();
            var isHealthy = await cacheService.IsHealthyAsync();
            return isHealthy ? HealthCheckResult.Healthy("Cache is working")
                             : HealthCheckResult.Degraded("Cache is not working, using fallback");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Cache check failed, using memory cache", ex);
        }
    }, tags: new[] { "cache", "performance" })
    .AddCheck("message_queue", async () =>
    {
        try
        {
            var mqService = builder.Services.BuildServiceProvider()
                .GetRequiredService<IMessageQueueService>();
            var isHealthy = await mqService.IsHealthyAsync();
            return isHealthy ? HealthCheckResult.Healthy("Message queue is working")
                             : HealthCheckResult.Degraded("Message queue is not available");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Message queue check failed", ex);
        }
    }, tags: new[] { "messaging", "performance" });

// Add file upload configuration
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 52428800; // 50MB
});

// Configure Kestrel
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 52428800; // 50MB
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
});

// Add Logging with enhanced configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (builder.Environment.IsProduction())
{
    builder.Logging.AddEventLog();
}

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    try
    {
        await DatabaseInitializer.InitializeAsync(scope.ServiceProvider);
        Console.WriteLine("✅ Database initialized successfully");

        // Start message queue consumer
        var mqService = scope.ServiceProvider.GetRequiredService<IMessageQueueService>();
        mqService.StartConsuming();
        Console.WriteLine("✅ Message queue consumer started");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Initialization failed: {ex.Message}");
        throw;
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee Disease Analysis API v1.1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        c.DefaultModelsExpandDepth(-1); // Hide models section by default

        // Custom CSS for better UI
        c.InjectStylesheet("/css/swagger-custom.css");
    });
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serve static files (for uploaded images and CSS)
app.UseStaticFiles();

// Add comprehensive security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-Robots-Tag", "noindex, nofollow");

    if (app.Environment.IsProduction())
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }

    await next();
});

app.UseRouting();

app.UseCors("AllowSpecificOrigins");

app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Enhanced health checks with detailed responses
app.MapHealthChecks("/health", new()
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            totalDuration = $"{report.TotalDuration.TotalMilliseconds:F2}ms",
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                description = x.Value.Description,
                duration = $"{x.Value.Duration.TotalMilliseconds:F2}ms",
                tags = x.Value.Tags?.ToArray() ?? Array.Empty<string>(),
                exception = x.Value.Exception?.Message
            }).ToArray(),
            summary = new
            {
                total = report.Entries.Count,
                healthy = report.Entries.Count(x => x.Value.Status == HealthStatus.Healthy),
                degraded = report.Entries.Count(x => x.Value.Status == HealthStatus.Degraded),
                unhealthy = report.Entries.Count(x => x.Value.Status == HealthStatus.Unhealthy)
            }
        };

        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }
});

// Lightweight health check for load balancers
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("critical")
});

app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false // Just returns 200 OK
});

// API Documentation route
app.MapGet("/", () => Results.Redirect("/swagger"));

// API Status endpoint
app.MapGet("/api/status", async (IServiceProvider serviceProvider) =>
{
    var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
    var predictionService = serviceProvider.GetRequiredService<IPredictionService>();

    try
    {
        var modelStats = await predictionService.GetCurrentModelInfoAsync();
        var totalPredictions = await context.Predictions.CountAsync();
        var totalUsers = await context.Users.CountAsync();
        var totalImages = await context.LeafImages.CountAsync();

        return Results.Ok(new
        {
            service = "Coffee Disease Analysis API",
            version = "1.1.0",
            status = "healthy",
            timestamp = DateTime.UtcNow,
            environment = app.Environment.EnvironmentName,
            statistics = new
            {
                totalPredictions,
                totalUsers,
                totalImages,
                currentModel = new
                {
                    modelStats.ModelName,
                    modelStats.Version,
                    modelStats.Accuracy,
                    modelStats.IsProduction
                }
            },
            features = new
            {
                cnn = "ResNet50 for disease classification",
                mlp = "Multi-layer perceptron for symptom analysis",
                asyncProcessing = "RabbitMQ message queue",
                caching = "Redis distributed cache",
                batchProcessing = "Multiple image analysis",
                feedback = "User feedback learning system"
            }
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Service Status Error",
            detail: ex.Message,
            statusCode: 500
        );
    }
});

// Global exception handling
app.UseExceptionHandler(appError =>
{
    appError.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var contextFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (contextFeature != null)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(contextFeature.Error, "Unhandled exception occurred: {Path}", context.Request.Path);

            var response = new
            {
                error = "Internal Server Error",
                message = app.Environment.IsDevelopment()
                    ? contextFeature.Error.Message
                    : "An error occurred while processing your request",
                timestamp = DateTime.UtcNow,
                path = context.Request.Path.Value,
                requestId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        }
    });
});

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        var mqService = app.Services.GetRequiredService<IMessageQueueService>();
        mqService.StopConsuming();
        Console.WriteLine("✅ Message queue consumer stopped gracefully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Error stopping message queue: {ex.Message}");
    }
});

Console.WriteLine("🚀 Coffee Disease Analysis API is starting...");
Console.WriteLine($"🌍 Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"📝 Swagger UI: {(app.Environment.IsDevelopment() ? "https://localhost:7179/swagger" : "Available in development only")}");
Console.WriteLine($"❤️ Health Check: /health");
Console.WriteLine($"📊 API Status: /api/status");

app.Run();

// Make the implicit Program class public for integration tests
public partial class Program { }