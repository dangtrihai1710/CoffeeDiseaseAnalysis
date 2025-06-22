// ===================================================================
// File: CoffeeDiseaseAnalysis/Program.cs - UPDATED WITH FIXED DTOS
// ===================================================================
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Extensions;
using CoffeeDiseaseAnalysis.Filters;
using CoffeeDiseaseAnalysis.Middleware;
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.IdentityModel.Tokens;
using System.IO.Compression;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("🚀 Starting Coffee Disease Analysis API...");
Console.WriteLine("📋 Checking configuration and dependencies...");

// ===================================================================
// 1. DATABASE CONFIGURATION
// ===================================================================
try
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    Console.WriteLine("✅ Database context configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Database configuration failed: {ex.Message}");
}

// ===================================================================
// 2. IDENTITY CONFIGURATION
// ===================================================================
try
{
    builder.Services.AddIdentity<User, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
    Console.WriteLine("✅ Identity configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Identity configuration failed: {ex.Message}");
}

// ===================================================================
// 3. JWT AUTHENTICATION
// ===================================================================
try
{
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                var result = System.Text.Json.JsonSerializer.Serialize(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Token xác thực là bắt buộc",
                    StatusCode = 401
                });

                return context.Response.WriteAsync(result);
            },

            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError($"JWT Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "CoffeeDiseaseAnalysis",
            ValidAudience = jwtSettings["Audience"] ?? "CoffeeDiseaseAnalysis",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

    builder.Services.AddAuthorization();
    Console.WriteLine("✅ JWT Authentication configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ JWT configuration failed: {ex.Message}");
}

// ===================================================================
// 4. CORS CONFIGURATION
// ===================================================================
try
{
    builder.Services.AddCustomCors();
    Console.WriteLine("✅ CORS configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ CORS configuration failed: {ex.Message}");
}

// ===================================================================
// 5. CACHE CONFIGURATION
// ===================================================================
try
{
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrEmpty(redisConnection))
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
        });
        Console.WriteLine("✅ Redis cache configured");
    }
    else
    {
        builder.Services.AddMemoryCache();
        Console.WriteLine("⚠️ Using memory cache (Redis not configured)");
    }
}
catch (Exception ex)
{
    builder.Services.AddMemoryCache();
    Console.WriteLine($"⚠️ Redis failed, using memory cache: {ex.Message}");
}

// ===================================================================
// 6. VALIDATION CONFIGURATION
// ===================================================================
try
{
    builder.Services.AddFluentValidationAutoValidation()
                   .AddFluentValidationClientsideAdapters();

    // Register validators from assembly
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // Custom API validation behavior
    builder.Services.AddCustomApiValidation();
    Console.WriteLine("✅ FluentValidation configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Validation configuration failed: {ex.Message}");
}

// ===================================================================
// 7. RESPONSE COMPRESSION
// ===================================================================
try
{
    builder.Services.AddResponseCompression(options =>
    {
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
            new[] { "application/json", "text/json" });
    });

    builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    {
        options.Level = CompressionLevel.Fastest;
    });

    builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    {
        options.Level = CompressionLevel.SmallestSize;
    });
    Console.WriteLine("✅ Response compression configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Response compression failed: {ex.Message}");
}

// ===================================================================
// 8. CORE SERVICES REGISTRATION
// ===================================================================
Console.WriteLine("📦 Registering core services...");
try
{
    builder.Services.AddScoped<IPredictionService, RealPredictionService>();
    Console.WriteLine("  ✅ IPredictionService -> RealPredictionService");
}
catch (Exception ex)
{
    Console.WriteLine($"  ❌ IPredictionService failed: {ex.Message}");
}

try
{
    builder.Services.AddScoped<ICacheService, CacheService>();
    Console.WriteLine("  ✅ ICacheService -> CacheService");
}
catch (Exception ex)
{
    Console.WriteLine($"  ❌ ICacheService failed: {ex.Message}");
}

try
{
    builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
    Console.WriteLine("  ✅ IImageProcessingService -> ImageProcessingService");
}
catch (Exception ex)
{
    Console.WriteLine($"  ❌ IImageProcessingService failed: {ex.Message}");
}

try
{
    builder.Services.AddScoped<IMessageQueueService, MessageQueueService>();
    Console.WriteLine("  ✅ IMessageQueueService -> MessageQueueService");
}
catch (Exception ex)
{
    Console.WriteLine($"  ❌ IMessageQueueService failed: {ex.Message}");
}

// ===================================================================
// 9. ADDITIONAL SERVICES
// ===================================================================
try
{
    builder.Services.AddScoped<IMLPService, MLPService>();
    builder.Services.AddScoped<IReportService, ReportService>();
    Console.WriteLine("  ✅ Additional services registered");
}
catch (Exception ex)
{
    Console.WriteLine($"  ❌ Additional services failed: {ex.Message}");
}

// ===================================================================
// 10. CONTROLLERS & API CONFIGURATION
// ===================================================================
try
{
    builder.Services.AddControllers(options =>
    {
        // Add global filters
        options.Filters.Add<ValidationFilter>();

        // Configure JSON options
        options.SuppressAsyncSuffixInActionNames = false;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
    });

    Console.WriteLine("✅ Controllers configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Controllers configuration failed: {ex.Message}");
}

// ===================================================================
// 11. SWAGGER CONFIGURATION
// ===================================================================
try
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title = "Coffee Disease Analysis API",
            Version = "v2.2-Complete",
            Description = "🤖 API phân tích bệnh lá cà phê với AI - Fixed DTOs & Validation"
        });

        c.AddSecurityDefinition("Bearer", new()
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
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

        // Include XML comments if available
        var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    });
    Console.WriteLine("✅ Swagger configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Swagger configuration failed: {ex.Message}");
}

// ===================================================================
// 12. HEALTH CHECKS
// ===================================================================
try
{
    builder.Services.AddHealthChecks()
        .AddDbContext<ApplicationDbContext>()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());
    Console.WriteLine("✅ Health checks configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Health checks failed: {ex.Message}");
}

Console.WriteLine("📝 Building application...");
var app = builder.Build();

Console.WriteLine("🔧 Configuring HTTP pipeline...");

// ===================================================================
// 13. MIDDLEWARE PIPELINE
// ===================================================================

// Exception handling middleware (first)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Development specific middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee Disease Analysis API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Coffee Disease Analysis API Documentation";
    });
    Console.WriteLine("✅ Swagger UI enabled");
}

// Response compression
app.UseResponseCompression();

// Security headers
app.Use((context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    return next();
});

// Core middleware
app.UseHttpsRedirection();

// CORS (before authentication)
var corsPolicy = app.Environment.IsDevelopment() ? "Development" : "Production";
app.UseCors(corsPolicy);

app.UseStaticFiles();
app.UseRouting();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Controllers
app.MapControllers();

// Health checks
app.MapHealthChecks("/health");

// ===================================================================
// 14. API ENDPOINTS
// ===================================================================

// Root endpoint
app.MapGet("/", () => new ApiResponse<object>
{
    Success = true,
    Message = "Coffee Disease Analysis API is running!",
    Data = new
    {
        version = "v2.2-Complete",
        timestamp = DateTime.UtcNow,
        swagger = "/swagger",
        health = "/health",
        status = "Fixed DTOs & Validation Implementation"
    }
});

// API info endpoint
app.MapGet("/api", () => new ApiResponse<object>
{
    Success = true,
    Message = "API Information",
    Data = new
    {
        endpoints = new
        {
            auth = "/api/auth",
            prediction = "/api/prediction",
            user = "/api/user",
            admin = "/api/admin"
        },
        features = new[]
        {
            "Real AI Disease Detection",
            "Batch Image Processing",
            "User Authentication & Authorization",
            "Prediction History & Analytics",
            "Fixed DTOs & Validation",
            "Error Handling & Logging"
        }
    }
});

// ===================================================================
// 15. STARTUP TASKS
// ===================================================================

// Run database migrations
Console.WriteLine("🗄️ Running database migrations...");
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
        Console.WriteLine("✅ Database migrations completed");

        // Seed default roles
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var roles = new[] { "Admin", "User", "Expert" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
        Console.WriteLine("✅ Default roles seeded");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database migration failed: {ex.Message}");
        Console.WriteLine("⚠️ Application will continue but database features may not work");
    }
}

// Create required directories
try
{
    var directories = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "models"),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "temp"),
        Path.Combine(Directory.GetCurrentDirectory(), "logs")
    };

    foreach (var dir in directories)
    {
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }
    Console.WriteLine("✅ Required directories created");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Could not create directories: {ex.Message}");
}

// ===================================================================
// 16. STARTUP COMPLETE
// ===================================================================

Console.WriteLine("\n🎉 Coffee Disease Analysis API Started Successfully!");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📊 Swagger UI: https://localhost:7179/swagger");
Console.WriteLine("🔗 API Base: https://localhost:7179/api");
Console.WriteLine("❤️ Health Check: https://localhost:7179/health");
Console.WriteLine("🏠 Root: https://localhost:7179/");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("✨ New Features:");
Console.WriteLine("  - Fixed DTOs and validation");
Console.WriteLine("  - Global exception handling");
Console.WriteLine("  - Response compression");
Console.WriteLine("  - Enhanced security headers");
Console.WriteLine("  - Improved error responses");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

app.Run();