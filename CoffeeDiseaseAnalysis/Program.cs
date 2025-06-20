// File: CoffeeDiseaseAnalysis/Program.cs - FIXED với Mock Services Fallback
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Services;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using CoffeeDiseaseAnalysis.Services.Mock;
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
    Console.WriteLine("🚀 Coffee Disease Analysis API v1.3 is starting...");

    // 1. CONFIGURE JSON OPTIONS
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    // 2. DATABASE CONFIGURATION
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=(localdb)\\mssqllocaldb;Database=CoffeeDiseaseAnalysisDb;Trusted_Connection=true;MultipleActiveResultSets=true";

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
        options.RequireHttpsMetadata = false;
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
        options.SizeLimit = 1000;
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

    // 6. APPLICATION SERVICES - Dùng Real AI Services với Model Thật
    Console.WriteLine("🤖 Registering Real AI Services with actual model...");

    // Core Services (luôn có)
    builder.Services.AddScoped<ICacheService, CacheService>();

    // Check if we should use real AI services
    var useRealAI = builder.Configuration.GetValue<bool>("UseRealAI", true);
    var modelPath = Path.Combine(builder.Environment.WebRootPath ?? "wwwroot", "models", "coffee_resnet50_v1.1.onnx");
    var modelExists = File.Exists(modelPath);

    Console.WriteLine($"📁 Model file check: {modelPath}");
    Console.WriteLine($"📊 Model exists: {modelExists}");
    Console.WriteLine($"⚙️ UseRealAI setting: {useRealAI}");

    if (useRealAI && modelExists)
    {
        Console.WriteLine("✅ Using REAL AI Services with coffee_resnet50_v1.1.onnx model");

        // Real AI Services với model thật
        builder.Services.AddScoped<IPredictionService, RealPredictionService>();
        builder.Services.AddScoped<IMLPService, MockMLPService>(); // MLP vẫn dùng mock
        builder.Services.AddScoped<IMessageQueueService, MockMessageQueueService>();
    }
    else
    {
        if (!modelExists)
        {
            Console.WriteLine($"⚠️ Model file not found at: {modelPath}");
            Console.WriteLine("📋 Falling back to Mock Services");
        }
        else
        {
            Console.WriteLine("📋 Using Mock Services (UseRealAI = false)");
        }

        // Mock Services fallback
        builder.Services.AddScoped<IPredictionService, MockPredictionService>();
        builder.Services.AddScoped<IMLPService, MockMLPService>();
        builder.Services.AddScoped<IMessageQueueService, MockMessageQueueService>();
    }

    // Business Services (mock implementations)
    builder.Services.AddScoped<IFileService, MockFileService>();
    builder.Services.AddScoped<INotificationService, MockNotificationService>();
    builder.Services.AddScoped<IEmailService, MockEmailService>();
    builder.Services.AddScoped<IReportService, MockReportService>();
    builder.Services.AddScoped<IModelManagementService, MockModelManagementService>();

    // Background Services
    builder.Services.AddHostedService<ModelTrainingBackgroundService>();

    // 7. CORS CONFIGURATION
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowReactApp", corsBuilder =>
        {
            corsBuilder
                .WithOrigins("http://localhost:3000", "https://localhost:3000")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .SetIsOriginAllowedToReturnTrue(origin => true); // Allow all origins in development
        });
    });

    // 8. FILE UPLOAD CONFIGURATION
    builder.Services.Configure<FormOptions>(options =>
    {
        options.ValueLengthLimit = int.MaxValue;
        options.MultipartBodyLengthLimit = 52428800; // 50MB
        options.MultipartHeadersLengthLimit = int.MaxValue;
    });

    // 9. API CONTROLLERS
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ModelValidationActionFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    // 10. SWAGGER/OpenAPI CONFIGURATION
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Coffee Disease Analysis API",
            Version = "v1.3",
            Description = "API để phân tích bệnh trên lá cà phê sử dụng AI/ML"
        });

        // JWT Authentication for Swagger
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
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

    // 11. HEALTH CHECKS
    builder.Services.AddHealthChecks()
        .AddDbContext<ApplicationDbContext>()
        .AddCheck("self", () => HealthCheckResult.Healthy("API is running"));

    // 12. RATE LIMITING
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("FileUpload", configure =>
        {
            configure.PermitLimit = 10;
            configure.Window = TimeSpan.FromMinutes(1);
            configure.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            configure.QueueLimit = 5;
        });
    });

    // BUILD THE APP
    var app = builder.Build();

    // MIDDLEWARE PIPELINE
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee Disease Analysis API v1.3");
            c.RoutePrefix = "swagger";
        });
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    // CORS - Must be before Authentication
    app.UseCors("AllowReactApp");

    // Rate Limiting
    app.UseRateLimiter();

    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Health Checks
    app.MapHealthChecks("/health");

    // API Controllers
    app.MapControllers();

    // Default redirect to Swagger in development
    app.MapGet("/", () => Results.Redirect(app.Environment.IsDevelopment() ? "/swagger" : "/api/status"));

    // DATABASE INITIALIZATION
    await InitializeDatabaseAsync(app);

    // STARTUP BANNER
    Console.WriteLine("============================================================");
    Console.WriteLine("🚀 Coffee Disease Analysis API v1.3");
    Console.WriteLine("============================================================");
    Console.WriteLine($"🌍 Environment: {app.Environment.EnvironmentName}");
    Console.WriteLine($"📝 Swagger UI: {(app.Environment.IsDevelopment() ? "https://localhost:7179/swagger" : "Disabled (Production)")}");
    Console.WriteLine($"💾 Cache: {(redisConnected ? "Redis + Memory" : "Memory Only")}");
    Console.WriteLine($"🔐 Authentication: ✅ JWT Bearer");
    Console.WriteLine($"🔗 CORS: ✅ Enabled for React App");
    Console.WriteLine($"📊 Health Checks: /health, /api/health");
    Console.WriteLine($"📋 Demo Accounts:");
    Console.WriteLine($"   👤 Admin: admin@coffeedisease.com / Admin123!");
    Console.WriteLine($"   🔬 Expert: expert@coffeedisease.com / Expert123!");
    Console.WriteLine($"   👤 User: user@demo.com / User123!");
    Console.WriteLine($"✨ Ready to analyze coffee leaf diseases! 🌱");
    Console.WriteLine("============================================================");

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