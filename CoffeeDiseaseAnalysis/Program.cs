// File: CoffeeDiseaseAnalysis/Program.cs - FIXED VERSION
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

// Fix: Register MessageQueueService as Scoped instead of Singleton to avoid disposal issues
builder.Services.AddScoped<IMessageQueueService, MessageQueueService>();

// Register PredictionService last (depends on other services)
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

// Add Health Checks with better error handling
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "database", "critical" })
    .AddCheck("cache", async () =>
    {
        try
        {
            using var scope = builder.Services.BuildServiceProvider().CreateScope();
            var cacheService = scope.ServiceProvider.GetService<ICacheService>();
            if (cacheService == null)
                return HealthCheckResult.Degraded("Cache service not available");

            var isHealthy = await cacheService.IsHealthyAsync();
            return isHealthy ? HealthCheckResult.Healthy("Cache is working")
                             : HealthCheckResult.Degraded("Cache degraded, using fallback");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded($"Cache check failed: {ex.Message}");
        }
    }, tags: new[] { "cache", "performance" });

// Configure file upload limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 52428800; // 50MB
});

var app = builder.Build();

// Initialize database with better error handling
try
{
    using var scope = app.Services.CreateScope();
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider);
    Console.WriteLine("✅ Database initialized successfully");

    // Start message queue consumer with error handling
    try
    {
        var mqService = scope.ServiceProvider.GetService<IMessageQueueService>();
        if (mqService != null)
        {
            mqService.StartConsuming();
            Console.WriteLine("✅ Message queue consumer started");
        }
        else
        {
            Console.WriteLine("⚠️ Message queue service not available");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Message queue initialization failed: {ex.Message}");
        Console.WriteLine("📝 Continuing without message queue");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Initialization failed: {ex.Message}");
    throw;
}

// Configure pipeline
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
    await next();
});

app.UseRouting();
app.UseCors("AllowSpecificOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health checks with simplified response
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("critical")
});

// API status endpoint
app.MapGet("/api/status", () => Results.Ok(new
{
    service = "Coffee Disease Analysis API",
    version = "1.1.0",
    status = "healthy",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}));

app.MapGet("/", () => Results.Redirect("/swagger"));

Console.WriteLine("🚀 Coffee Disease Analysis API is starting...");
Console.WriteLine($"🌍 Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"📝 Swagger UI: /swagger");
Console.WriteLine($"❤️ Health Check: /health");

app.Run();