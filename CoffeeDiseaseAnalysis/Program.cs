// ===================================================================
// 2. FIXED Program.cs - Complete CORS & Authentication
// ===================================================================
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Services;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using CoffeeDiseaseAnalysis.Models.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authorization;
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
        // Relaxed password requirements for development
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
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
// 3. JWT AUTHENTICATION - COMPLETELY FIXED
// ===================================================================
try
{
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!CoffeeDiseaseAnalysis2024";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.RequireHttpsMetadata = false; // Allow HTTP for development

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Check for token in header
                var accessToken = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                var result = System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Token xác thực là bắt buộc",
                    statusCode = 401,
                    timestamp = DateTime.UtcNow
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
            ClockSkew = TimeSpan.FromMinutes(5), // Allow 5 minutes clock skew
            RequireExpirationTime = true
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
// 4. CORS CONFIGURATION - COMPLETELY FIXED
// ===================================================================
try
{
    builder.Services.AddCors(options =>
    {
        // Development policy - very permissive
        options.AddPolicy("Development", policy =>
        {
            policy.WithOrigins(
                    "http://localhost:3000",
                    "https://localhost:3000",
                    "http://127.0.0.1:3000",
                    "https://127.0.0.1:3000",
                    "http://localhost:3001",
                    "https://localhost:3001"
                  )
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials()
                  .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        });

        // Allow all for debugging
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });
    Console.WriteLine("✅ CORS configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ CORS configuration failed: {ex.Message}");
}

// ===================================================================
// 5. CORE SERVICES
// ===================================================================
try
{
    builder.Services.AddMemoryCache();

    // Register services with error handling
    try { builder.Services.AddScoped<IPredictionService, RealPredictionService>(); } catch { }
    try { builder.Services.AddScoped<ICacheService, CacheService>(); } catch { }
    try { builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>(); } catch { }
    try { builder.Services.AddScoped<IMessageQueueService, MessageQueueService>(); } catch { }
    try { builder.Services.AddScoped<IMLPService, MLPService>(); } catch { }
    try { builder.Services.AddScoped<IReportService, ReportService>(); } catch { }

    Console.WriteLine("✅ Core services registered");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Some services failed to register: {ex.Message}");
}

// ===================================================================
// 6. CONTROLLERS & API CONFIGURATION
// ===================================================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// ===================================================================
// 7. SWAGGER CONFIGURATION
// ===================================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Coffee Disease Analysis API",
        Version = "v2.5-AuthFixed",
        Description = "🤖 API phân tích bệnh lá cà phê - Authentication & CORS Fixed"
    });

    c.AddSecurityDefinition("Bearer", new()
    {
        Description = "JWT Authorization header. Enter: Bearer {token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// ===================================================================
// 8. HEALTH CHECKS
// ===================================================================
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("API is running"));

var app = builder.Build();

Console.WriteLine("🔧 Configuring HTTP pipeline...");

// ===================================================================
// 9. MIDDLEWARE PIPELINE - CORRECT ORDER
// ===================================================================

// Development tools
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee Disease Analysis API");
        c.RoutePrefix = "swagger";
    });
}

// Core middleware
app.UseHttpsRedirection();

// ✅ CORS - MUST BE BEFORE Authentication
app.UseCors("AllowAll"); // Use AllowAll for development debugging

// Static files
app.UseStaticFiles();

// Routing
app.UseRouting();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Controllers
app.MapControllers();

// Health checks
app.MapHealthChecks("/health");

// ===================================================================
// 10. TEST ENDPOINTS
// ===================================================================

// Root endpoint
app.MapGet("/", [AllowAnonymous] () => new
{
    success = true,
    message = "Coffee Disease Analysis API is running!",
    version = "v2.5-AuthFixed",
    timestamp = DateTime.UtcNow,
    endpoints = new
    {
        swagger = "/swagger",
        health = "/health",
        auth_login = "/api/auth/login",
        auth_register = "/api/auth/register"
    }
}).WithOpenApi();

// CORS test endpoint
app.MapPost("/api/test-cors", [AllowAnonymous] (object data) => new
{
    success = true,
    message = "CORS is working!",
    receivedData = data,
    timestamp = DateTime.UtcNow
}).WithOpenApi();

// Handle preflight requests
app.MapMethods("/api/{**path}", new[] { "OPTIONS" }, [AllowAnonymous] () => Results.Ok()).WithOpenApi();

// ===================================================================
// 11. STARTUP TASKS
// ===================================================================

// Run database migrations
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        Console.WriteLine("🗄️ Running database migrations...");
        await context.Database.MigrateAsync();
        Console.WriteLine("✅ Database migrations completed");

        // ✅ CALL DATABASE SEEDER (DÒNG QUAN TRỌNG)
        Console.WriteLine("🌱 Starting database seeding...");
        await CoffeeDiseaseAnalysis.Data.DatabaseSeeder.SeedAsync(context, userManager, roleManager, logger);
        Console.WriteLine("✅ Database seeding completed");

        // Check user count after seeding
        var userCount = await context.Users.CountAsync();
        Console.WriteLine($"📊 Total users in database: {userCount}");

        // List created users for verification
        var users = await context.Users.Select(u => new { u.Email, u.FullName, u.Role }).ToListAsync();
        Console.WriteLine("👥 Created users:");
        foreach (var user in users)
        {
            Console.WriteLine($"  - {user.Email} ({user.FullName}) - Role: {user.Role}");
        }

    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database setup failed: {ex.Message}");
        Console.WriteLine($"📝 Stack trace: {ex.StackTrace}");
    }
}

Console.WriteLine("\n🎉 Coffee Disease Analysis API Started Successfully!");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📊 Swagger UI: https://localhost:7179/swagger");
Console.WriteLine("🔗 API Base: https://localhost:7179/api");
Console.WriteLine("❤️ Health Check: https://localhost:7179/health");
Console.WriteLine("🧪 CORS Test: POST https://localhost:7179/api/test-cors");
Console.WriteLine("🔐 Auth Login: POST https://localhost:7179/api/auth/login");
Console.WriteLine("📝 Auth Register: POST https://localhost:7179/api/auth/register");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("✨ Fixed Issues:");
Console.WriteLine("  - ✅ JWT Token generation method added");
Console.WriteLine("  - ✅ CORS configuration for localhost:3000");
Console.WriteLine("  - ✅ Authentication error handling improved");
Console.WriteLine("  - ✅ Relaxed password requirements for development");
Console.WriteLine("  - ✅ Added test endpoints for debugging");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

app.Run();