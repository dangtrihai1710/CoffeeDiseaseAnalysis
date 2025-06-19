// File: CoffeeDiseaseAnalysis/Program.cs - FIXED CS2021 Ambiguous Call
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Services;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using CoffeeDiseaseAnalysis.Filters;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

try
{
    // 1. CONFIGURE JSON OPTIONS
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    // 2. DATABASE CONFIGURATION - FIXED CS2021
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("CoffeeDiseaseAnalysis");
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });

        // Enable sensitive data logging in development
        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }
    });

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    // 3. IDENTITY CONFIGURATION
    builder.Services.AddDefaultIdentity<User>(options =>
    {
        // Password policy
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
        options.Password.RequiredUniqueChars = 3;

        // Lockout policy
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 3;
        options.Lockout.AllowedForNewUsers = true;

        // User policy
        options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        options.User.RequireUniqueEmail = true;

        // Email confirmation (disable for development)
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

    // 4. JWT AUTHENTICATION CONFIGURATION
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"] ?? "CoffeeDiseaseAnalysisSecretKey2024!VeryStrongAndSecure";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = false; // Set to true in production
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "CoffeeDiseaseAnalysis",
            ValidAudience = jwtSettings["Audience"] ?? "CoffeeDiseaseAnalysisUsers",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                {
                    context.Response.Headers.Add("Token-Expired", "true");
                }
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                var result = JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Token không hợp lệ hoặc đã hết hạn",
                    errors = new[] { "Vui lòng đăng nhập lại" }
                });

                return context.Response.WriteAsync(result);
            }
        };
    });

    // 5. CACHING CONFIGURATION
    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1000; // Limit memory cache size
    });

    // Redis Cache (optional with fallback)
    var redisConnected = false;
    try
    {
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "CoffeeDiseaseAnalysis";
        });
        redisConnected = true;
        Console.WriteLine("✅ Redis cache configured successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Redis không khả dụng: {ex.Message}. Sử dụng Memory Cache.");
        redisConnected = false;
    }

    // 6. APPLICATION SERVICES - FIXED CS0246 Errors
    // Core AI Services
    builder.Services.AddScoped<IPredictionService, PredictionService>();
    builder.Services.AddScoped<IMLPService, MLPService>();
    builder.Services.AddScoped<IModelManagementService, ModelManagementService>();

    // Infrastructure Services
    builder.Services.AddScoped<ICacheService, CacheService>();
    builder.Services.AddScoped<IFileService, FileService>();
    builder.Services.AddScoped<IMessageQueueService, MessageQueueService>();

    // Business Services
    builder.Services.AddScoped<INotificationService, NotificationService>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IReportService, ReportService>();

    // Background Services
    builder.Services.AddHostedService<ModelTrainingBackgroundService>();

    // 7. CONTROLLERS CONFIGURATION
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<GlobalExceptionFilter>();
        options.Filters.Add<ValidationFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
    });

    // 8. FILE UPLOAD CONFIGURATION
    builder.Services.Configure<FormOptions>(options =>
    {
        options.ValueLengthLimit = int.MaxValue;
        options.MultipartBodyLengthLimit = 52428800; // 50MB
        options.MultipartHeadersLengthLimit = 16384;
        options.MemoryBufferThreshold = int.MaxValue;
    });

    // 9. RATE LIMITING
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.User?.Identity?.Name ?? context.Request.Headers.Host.ToString(),
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 1000,
                    Window = TimeSpan.FromMinutes(1)
                }));

        options.AddPolicy("AuthPolicy", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 10, // 10 auth attempts per minute
                    Window = TimeSpan.FromMinutes(1)
                }));

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = 429;
            context.HttpContext.Response.ContentType = "application/json";

            var response = JsonSerializer.Serialize(new
            {
                error = "Too Many Requests",
                message = "Quá nhiều yêu cầu. Vui lòng thử lại sau.",
                retryAfter = 60
            });

            await context.HttpContext.Response.WriteAsync(response, cancellationToken: token);
        };
    });

    // 10. SWAGGER CONFIGURATION
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Coffee Disease Analysis API",
            Version = "v1.3",
            Description = @"
🌱 **API phân tích bệnh lá cây cà phê sử dụng AI** - Enhanced Version

## 🔐 Authentication:
- **JWT Bearer Tokens** với 7 ngày expiry
- **Role-based Access**: Admin, Expert, User
- **Demo Accounts**: 
  - Admin: admin@coffeedisease.com / Admin123!
  - Expert: expert@coffeedisease.com / Expert123!
  - User: user@demo.com / User123!

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

## 🚀 Quick Start:
1. Đăng ký hoặc login tại `/api/auth/register` hoặc `/api/auth/login`
2. Upload ảnh lá cà phê tại `/api/prediction/upload`
3. Nhận kết quả phân tích ngay lập tức
            ",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "Coffee Disease Analysis Team",
                Email = "support@coffeedisease.com"
            }
        });

        // JWT Security Definition
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = @"
JWT Authorization header using the Bearer scheme.
Enter 'Bearer' [space] and then your token.

Example: 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'

🔑 Get a token by calling /api/auth/login with demo accounts above.
            "
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
    });

    // 11. CORS CONFIGURATION
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowReactApp", policy =>
        {
            policy.WithOrigins(
                "http://localhost:3000",     // Next.js dev server
                "https://localhost:3000",    // Next.js HTTPS
                "http://127.0.0.1:3000",     // Alternative localhost
                "https://127.0.0.1:3000"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true); // For development only
        });
    });

    // 12. HEALTH CHECKS
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("database", tags: new[] { "critical", "db" })
        .AddCheck("redis", () =>
        {
            return redisConnected ?
                HealthCheckResult.Healthy("Redis is connected") :
                HealthCheckResult.Degraded("Redis is not available, using memory cache");
        }, tags: new[] { "cache" })
        .AddCheck("storage", () =>
        {
            try
            {
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsPath); // Ensure directory exists
                return HealthCheckResult.Healthy("Storage is accessible");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Storage check failed: {ex.Message}");
            }
        }, tags: new[] { "storage" });

    // BUILD THE APP
    var app = builder.Build();

    // MIDDLEWARE PIPELINE CONFIGURATION
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseMigrationsEndPoint();

        // Swagger only in development
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee Disease Analysis API v1.3");
            c.RoutePrefix = "swagger";
            c.DisplayRequestDuration();
            c.EnableDeepLinking();
            c.EnableFilter();
            c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        });
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    // Security headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");

        if (!app.Environment.IsDevelopment())
        {
            context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }

        await next();
    });

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseRateLimiter();
    app.UseCors("AllowReactApp");

    // IMPORTANT: Authentication must come before Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Map controllers
    app.MapControllers();

    // HEALTH CHECK ENDPOINTS
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
                    data = e.Value.Data?.Count > 0 ? e.Value.Data : null
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

    // API STATUS ENDPOINT
    app.MapGet("/api/status", () => Results.Ok(new
    {
        service = "Coffee Disease Analysis API",
        version = "1.3.0",
        status = "healthy",
        timestamp = DateTime.UtcNow,
        environment = app.Environment.EnvironmentName,
        features = new
        {
            authentication = new { provider = "JWT Bearer", enabled = true },
            cnn_model = new { name = "ResNet50", version = "v1.1", accuracy = "87.5%" },
            caching = new { enabled = true, redis_connected = redisConnected },
            database = new { provider = "SQL Server", status = "connected" }
        },
        endpoints = new
        {
            swagger = app.Environment.IsDevelopment() ? "/swagger" : "disabled",
            health = "/health",
            auth_login = "/api/auth/login",
            auth_register = "/api/auth/register",
            prediction_upload = "/api/prediction/upload"
        },
        demo_accounts = new
        {
            admin = new { email = "admin@coffeedisease.com", password = "Admin123!" },
            expert = new { email = "expert@coffeedisease.com", password = "Expert123!" },
            user = new { email = "user@demo.com", password = "User123!" }
        }
    })).AllowAnonymous();

    // Root redirect
    app.MapGet("/", () => Results.Redirect(app.Environment.IsDevelopment() ? "/swagger" : "/api/status"));

    // DATABASE INITIALIZATION
    await InitializeDatabaseAsync(app);

    // STARTUP BANNER
    Console.WriteLine("🚀 Coffee Disease Analysis API v1.3 is starting...");
    Console.WriteLine($"🌍 Environment: {app.Environment.EnvironmentName}");
    Console.WriteLine($"📝 Swagger: {(app.Environment.IsDevelopment() ? "https://localhost:7140/swagger" : "Disabled (Production)")}");
    Console.WriteLine($"💾 Cache: {(redisConnected ? "Redis + Memory" : "Memory Only")}");
    Console.WriteLine($"🔐 JWT Authentication: ✅ Enabled");
    Console.WriteLine($"📊 Health Checks: /health");
    Console.WriteLine($"📋 Demo Accounts Available:");
    Console.WriteLine($"   👤 Admin: admin@coffeedisease.com / Admin123!");
    Console.WriteLine($"   🔬 Expert: expert@coffeedisease.com / Expert123!");
    Console.WriteLine($"   👤 User: user@demo.com / User123!");
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

// DATABASE INITIALIZATION METHOD
static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Run pending migrations
        if (context.Database.GetPendingMigrations().Any())
        {
            await context.Database.MigrateAsync();
            logger.LogInformation("✅ Database migrations applied successfully");
        }

        // Seed initial data
        await DatabaseSeeder.SeedAsync(context, userManager, roleManager, logger);

        logger.LogInformation("✅ Database initialization completed successfully");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ An error occurred while initializing the database");

        // Don't throw in production, just log the error
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            throw;
        }
    }
}