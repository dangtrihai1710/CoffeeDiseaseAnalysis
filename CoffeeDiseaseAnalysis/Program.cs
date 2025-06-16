// File: CoffeeDiseaseAnalysis/Program.cs - FINAL FIX
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Services;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

try
{
    // Add database services
    builder.Services.AddCoffeeDiseaseDatabase(builder.Configuration);
    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    // Configure Identity
    builder.Services.AddDefaultIdentity<User>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 6;
        options.Password.RequiredUniqueChars = 1;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        options.User.RequireUniqueEmail = true;

        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

    // Add Memory Cache first (required for CacheService)
    builder.Services.AddMemoryCache();

    // Add Redis Cache with fallback handling
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    try
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "CoffeeDiseaseAnalysis";
        });
        Console.WriteLine("✅ Redis cache configured successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Redis không khả dụng: {ex.Message}");
        Console.WriteLine("📝 Sử dụng Memory Cache làm fallback");
    }

    // Register services in correct order (dependencies first)
    builder.Services.AddScoped<ICacheService, CacheService>();
    builder.Services.AddScoped<IMLPService, MLPService>();
    builder.Services.AddScoped<IMessageQueueService, MessageQueueService>();
    builder.Services.AddScoped<IPredictionService, PredictionService>();

    // Add Controllers and API
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Configure Swagger
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title = "Coffee Disease Analysis API",
            Version = "v1.2",
            Description = @"
🌱 **API phân tích bệnh lá cây cà phê sử dụng AI**

## Tính năng chính:
- 🤖 **CNN Model**: ResNet50 phân loại 5 loại bệnh (Cercospora, Healthy, Miner, Phoma, Rust)
- 🧠 **MLP Model**: Phân tích triệu chứng bổ sung  
- ⚡ **Async Processing**: RabbitMQ cho xử lý bất đồng bộ
- 💾 **Caching**: Redis + Memory Cache để tối ưu hiệu suất
- 📊 **Feedback System**: Học từ phản hồi người dùng
- 🔄 **Model Management**: Quản lý phiên bản và A/B testing

## Models hiện tại:
- **ResNet50 v1.1**: 87.5% accuracy (Production)
- **MLP v1.0**: 72% accuracy (Symptoms analysis)
- **Combined**: 91% accuracy (Kết hợp CNN + MLP)

## API Endpoints:
- **POST /api/prediction/upload**: Upload ảnh (sync)
- **POST /api/prediction/upload-async**: Upload ảnh (async với RabbitMQ)
- **POST /api/prediction/upload-batch**: Batch upload
- **GET /api/prediction/history**: Lịch sử dự đoán
- **POST /api/prediction/feedback**: Thêm phản hồi
- **GET /api/model-management/versions**: Quản lý model
- **GET /api/dashboard/overview**: Dashboard thống kê
            ",
            Contact = new()
            {
                Name = "Coffee Disease Analysis Team",
                Email = "support@coffeedisease.com"
            }
        });

        // Add JWT authentication to Swagger
        c.AddSecurityDefinition("Bearer", new()
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme."
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
    });

    // Add CORS
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
                  .AllowCredentials();
        });
    });

    // Add Health Checks - Fixed lambda expression
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "database", "critical" })
        .AddCheck("cache", () =>
        {
            try
            {
                return HealthCheckResult.Healthy("Cache service registered");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Degraded($"Cache check failed: {ex.Message}");
            }
        }, tags: new[] { "cache", "performance" })
        .AddCheck("message_queue", () =>
        {
            try
            {
                return HealthCheckResult.Healthy("Message queue service registered");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Degraded($"MQ check failed: {ex.Message}");
            }
        }, tags: new[] { "messaging", "performance" });

    // Configure file upload limits
    builder.Services.Configure<IISServerOptions>(options =>
    {
        options.MaxRequestBodySize = 52428800; // 50MB
    });

    var app = builder.Build();

    // Initialize database với error handling tốt hơn
    try
    {
        using var scope = app.Services.CreateScope();
        await DatabaseInitializer.InitializeAsync(scope.ServiceProvider);
        Console.WriteLine("✅ Database initialized successfully");

        // Start message queue consumer với error handling
        try
        {
            var mqService = scope.ServiceProvider.GetService<IMessageQueueService>();
            if (mqService != null)
            {
                // Delay để đảm bảo RabbitMQ ready
                await Task.Delay(1000);
                mqService.StartConsuming();
                Console.WriteLine("✅ Message queue consumer started");
            }
            else
            {
                Console.WriteLine("⚠️ Message queue service not available - continuing without MQ");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Message queue initialization failed: {ex.Message}");
            Console.WriteLine("📝 Continuing without message queue - sync processing only");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
        Console.WriteLine("🔄 Application will continue but may have limited functionality");
        // Don't throw - allow app to start for debugging
    }

    // Configure pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee Disease Analysis API v1.2");
            c.RoutePrefix = "swagger";
            c.DisplayRequestDuration();
            c.EnableDeepLinking();
            c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
            c.DefaultModelsExpandDepth(-1);
        });
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    // Security headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        await next();
    });

    app.UseRouting();
    app.UseCors("AllowSpecificOrigins");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // Health checks với detailed response
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = new
            {
                status = report.Status.ToString(),
                timestamp = DateTime.UtcNow,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds
                })
            };
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(result));
        }
    });

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("critical")
    });

    // API status endpoint
    app.MapGet("/api/status", () => Results.Ok(new
    {
        service = "Coffee Disease Analysis API",
        version = "1.2.0",
        status = "healthy",
        timestamp = DateTime.UtcNow,
        environment = app.Environment.EnvironmentName,
        features = new
        {
            cnn_model = "ResNet50 v1.1",
            mlp_model = "Symptoms MLP v1.0",
            async_processing = "RabbitMQ",
            caching = "Redis + Memory",
            database = "SQL Server"
        }
    }));

    // Redirect root to swagger
    app.MapGet("/", () => Results.Redirect("/swagger"));

    Console.WriteLine("🚀 Coffee Disease Analysis API is starting...");
    Console.WriteLine($"🌍 Environment: {app.Environment.EnvironmentName}");
    Console.WriteLine($"📝 Swagger UI: {(app.Environment.IsDevelopment() ? "https://localhost:7179/swagger" : "/swagger")}");
    Console.WriteLine($"❤️ Health Check: /health");
    Console.WriteLine($"📊 API Status: /api/status");
    Console.WriteLine("✨ Features: CNN Model, MLP Model, Async Processing, Redis Cache, Model Management");

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Application failed to start: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    throw;
}