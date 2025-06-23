// ===================================================================
// CoffeeDiseaseAnalysis/Program.cs - UPDATED WITH NEW SERVICES
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
// 2. MEMORY CACHE - REQUIRED FOR OTP SERVICE
// ===================================================================
builder.Services.AddMemoryCache();
Console.WriteLine("✅ Memory cache configured");

// ===================================================================
// 3. IDENTITY CONFIGURATION
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
// 4. JWT AUTHENTICATION - COMPLETELY FIXED
// ===================================================================
try
{
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"] ?? "CoffeeDiseaseAnalysis_SuperSecretKey_2024_Development_Only";
    var issuer = jwtSettings["Issuer"] ?? "CoffeeDiseaseAnalysis";
    var audience = jwtSettings["Audience"] ?? "CoffeeDiseaseAnalysisUsers";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

    Console.WriteLine("✅ JWT Authentication configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ JWT configuration failed: {ex.Message}");
}

// ===================================================================
// 5. AUTHORIZATION
// ===================================================================
builder.Services.AddAuthorization();

// ===================================================================
// 6. CORS CONFIGURATION - FIXED FOR LOCALHOST:3000
// ===================================================================
try
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowNextJSApp", policy =>
        {
            policy.WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000",
                "http://127.0.0.1:3000",
                "https://127.0.0.1:3000"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
        });
    });
    Console.WriteLine("✅ CORS configured for localhost:3000");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ CORS configuration failed: {ex.Message}");
}

// ===================================================================
// 7. SERVICES REGISTRATION - NEW SERVICES ADDED
// ===================================================================
try
{
    // ✅ Email Service
    builder.Services.AddScoped<IEmailService, EmailService>();
    Console.WriteLine("✅ Email Service registered");

    // ✅ OTP Service - NEW
    builder.Services.AddScoped<IOtpService, OtpService>();
    Console.WriteLine("✅ OTP Service registered");

    // Other existing services...
    builder.Services.AddScoped<IPredictionService, RealPredictionService>();
    builder.Services.AddScoped<IDashboardService, DashboardService>();
    Console.WriteLine("✅ All services registered");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Services registration failed: {ex.Message}");
}

// ===================================================================
// 8. HEALTH CHECKS
// ===================================================================
try
{
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>("database")
        .AddCheck("email", () => HealthCheckResult.Healthy("Email service is running"))
        .AddCheck("otp", () => HealthCheckResult.Healthy("OTP service is running"));
    Console.WriteLine("✅ Health checks configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Health checks configuration failed: {ex.Message}");
}

// ===================================================================
// 9. CONTROLLERS AND API
// ===================================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ===================================================================
// 10. LOGGING
// ===================================================================
builder.Services.AddLogging();

var app = builder.Build();

// ===================================================================
// MIDDLEWARE PIPELINE
// ===================================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ✅ CORS MUST BE BEFORE Authentication
app.UseCors("AllowNextJSApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// ===================================================================
// TEST ENDPOINTS
// ===================================================================
app.MapPost("/api/test-cors", [AllowAnonymous] (HttpContext context) =>
{
    return Results.Ok(new
    {
        message = "CORS test successful",
        origin = context.Request.Headers.Origin.ToString(),
        timestamp = DateTime.UtcNow
    });
});

// ===================================================================
// ROLE SEEDING
// ===================================================================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roles = { "Admin", "User" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                Console.WriteLine($"✅ Role '{role}' created");
            }
        }

        Console.WriteLine("✅ Role seeding completed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Role seeding failed: {ex.Message}");
    }
}

// ===================================================================
// STARTUP MESSAGES
// ===================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("🌿 COFFEE DISEASE ANALYSIS API - ENHANCED");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📊 Swagger UI: https://localhost:7179/swagger");
Console.WriteLine("🔗 API Base: https://localhost:7179/api");
Console.WriteLine("❤️ Health Check: https://localhost:7179/health");
Console.WriteLine("🧪 CORS Test: POST https://localhost:7179/api/test-cors");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("🔐 Auth Endpoints:");
Console.WriteLine("  - POST /api/Auth/login");
Console.WriteLine("  - POST /api/Auth/register");
Console.WriteLine("  - POST /api/Auth/forgot-password ✨ NEW");
Console.WriteLine("  - POST /api/Auth/verify-otp ✨ NEW");
Console.WriteLine("  - POST /api/Auth/reset-password ✨ NEW");
Console.WriteLine("  - POST /api/Auth/change-password");
Console.WriteLine("  - GET  /api/Auth/me");
Console.WriteLine("  - POST /api/Auth/logout");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("✨ New Features Added:");
Console.WriteLine("  - ✅ OTP Service with memory cache");
Console.WriteLine("  - ✅ Enhanced Email Service with HTML templates");
Console.WriteLine("  - ✅ Forgot Password with OTP verification");
Console.WriteLine("  - ✅ Password Reset flow");
Console.WriteLine("  - ✅ Email notifications");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

app.Run();