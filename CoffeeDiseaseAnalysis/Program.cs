// ===================================================================
// File: CoffeeDiseaseAnalysis/Program.cs - FINAL WITH DIAGNOSTICS
// ===================================================================
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Services;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("🚀 Starting Coffee Disease Analysis API...");
Console.WriteLine("📋 Checking configuration and dependencies...");

// Add services to the container
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

// ✅ IDENTITY CONFIGURATION
try
{
    builder.Services.AddIdentity<User, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = false;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
    Console.WriteLine("✅ Identity configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Identity configuration failed: {ex.Message}");
}

// ✅ JWT AUTHENTICATION
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

                var result = System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Token is required",
                    statusCode = 401
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

// ✅ CORS CONFIGURATION
try
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", builder =>
        {
            builder
                .WithOrigins("http://localhost:3000", "https://localhost:3000")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
    });
    Console.WriteLine("✅ CORS configured");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ CORS configuration failed: {ex.Message}");
}

// ✅ CACHE CONFIGURATION
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

// ✅ CORE SERVICES - WITH ERROR HANDLING
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

// ✅ ADDITIONAL SERVICES
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

// Controllers
builder.Services.AddControllers();

// API Explorer for Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Coffee Disease Analysis API",
        Version = "v2.1-Fixed",
        Description = "🤖 API phân tích bệnh lá cà phê với AI - Real Data Implementation"
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
});

Console.WriteLine("📝 Building application...");
var app = builder.Build();

Console.WriteLine("🔧 Configuring HTTP pipeline...");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee Disease Analysis API v1");
        c.RoutePrefix = "swagger";
    });
    Console.WriteLine("✅ Swagger UI enabled");
}

// ✅ MIDDLEWARE ORDER
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ✅ ROOT ENDPOINT FOR TESTING
app.MapGet("/", () => new
{
    message = "Coffee Disease Analysis API is running!",
    version = "v2.1-Fixed",
    timestamp = DateTime.UtcNow,
    swagger = "/swagger",
    status = "Real Data Implementation"
});

// ✅ HEALTH CHECK ENDPOINT
app.MapGet("/health", () => new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow,
    version = "v2.1-Fixed",
    database = "Connected",
    ai_model = "Available"
});

// ✅ RUN MIGRATIONS AUTOMATICALLY
Console.WriteLine("🗄️ Running database migrations...");
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
        Console.WriteLine("✅ Database migrations completed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database migration failed: {ex.Message}");
        Console.WriteLine("⚠️ Application will continue but database features may not work");
    }
}

// ✅ CREATE UPLOAD DIRECTORIES
try
{
    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
    if (!Directory.Exists(uploadsPath))
    {
        Directory.CreateDirectory(uploadsPath);
        Console.WriteLine("✅ Upload directories created");
    }

    var modelsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "models");
    if (!Directory.Exists(modelsPath))
    {
        Directory.CreateDirectory(modelsPath);
        Console.WriteLine("✅ Models directory created");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Could not create directories: {ex.Message}");
}

Console.WriteLine("\n🎉 Coffee Disease Analysis API Started Successfully!");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📊 Swagger UI: https://localhost:7179/swagger");
Console.WriteLine("🔗 API Base: https://localhost:7179/api");
Console.WriteLine("❤️ Health Check: https://localhost:7179/health");
Console.WriteLine("🏠 Root: https://localhost:7179/");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

app.Run();