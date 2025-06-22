// File: CoffeeDiseaseAnalysis/Program.cs - COMPLETE FIXED VERSION
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Services;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity configuration
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ✅ MEMORY CACHE (REQUIRED)
builder.Services.AddMemoryCache();

// ✅ REDIS CACHE (OPTIONAL)
try
{
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrEmpty(redisConnection))
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "CoffeeDiseaseAnalysis";
        });

        Console.WriteLine("✅ Redis cache configured");
    }
    else
    {
        Console.WriteLine("⚠️ Redis connection string not found, using memory cache only");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Redis connection failed: {ex.Message}. Using memory cache only.");
}

// ✅ CORE SERVICES
builder.Services.AddScoped<IPredictionService, RealPredictionService>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
builder.Services.AddScoped<IMessageQueueService, MessageQueueService>();

// Controllers
builder.Services.AddControllers();

// API Explorer for Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Coffee Disease Analysis API",
        Version = "v2.1-Complete",
        Description = "🤖 API phân tích bệnh lá cà phê với AI - Tất cả lỗi đã được fix"
    });

    c.AddSecurityDefinition("Bearer", new()
    {
        Description = "JWT Authorization header using the Bearer scheme. Ví dụ: 'Bearer eyJhbGciOiJIUzI1...'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
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

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
    config.SetMinimumLevel(LogLevel.Information);
});

// ✅ HEALTH CHECKS (Optional but recommended)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee Disease Analysis API v2.1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Coffee Disease Analysis - Complete API";
        c.DefaultModelExpandDepth(2);
        c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
    });
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// ✅ STATIC FILES SETUP
app.UseStaticFiles();

// Tạo thư mục uploads và models nếu chưa có
var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
var modelsPath = Path.Combine(app.Environment.WebRootPath, "models");

if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
    Console.WriteLine($"✅ Created uploads directory: {uploadsPath}");
}

if (!Directory.Exists(modelsPath))
{
    Directory.CreateDirectory(modelsPath);
    Console.WriteLine($"✅ Created models directory: {modelsPath}");
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ✅ HEALTH CHECK ENDPOINT
app.MapHealthChecks("/health");

// ✅ SIMPLE ROOT ENDPOINT
app.MapGet("/", () => new
{
    name = "Coffee Disease Analysis API",
    version = "v2.1",
    status = "✅ Running",
    swagger = "/swagger",
    health = "/health",
    timestamp = DateTime.UtcNow
});

// ✅ SEED DATABASE
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await SeedDataAsync(context, userManager, roleManager);
        Console.WriteLine("✅ Database seeded successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database seeding failed: {ex.Message}");
    }
}

// ✅ START MESSAGE QUEUE (If configured)
try
{
    using var scope = app.Services.CreateScope();
    var messageQueue = scope.ServiceProvider.GetService<IMessageQueueService>();
    if (messageQueue != null)
    {
        var health = await messageQueue.IsHealthyAsync();
        if (health)
        {
            messageQueue.StartConsuming();
            Console.WriteLine("✅ Message queue started");
        }
        else
        {
            Console.WriteLine("⚠️ Message queue not healthy, running without queue");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Message queue initialization failed: {ex.Message}");
}

Console.WriteLine("🚀 Coffee Disease Analysis API is ready!");
Console.WriteLine($"🌐 Swagger UI: {(app.Environment.IsDevelopment() ? "https://localhost:7179/swagger" : "/swagger")}");

app.Run();

// ===================================================================
// SEED METHOD - PRODUCTION READY
// ===================================================================
static async Task SeedDataAsync(
    ApplicationDbContext context,
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager)
{
    // Ensure database exists
    await context.Database.EnsureCreatedAsync();

    // Create roles
    var roles = new[] { "Admin", "Expert", "User" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Create admin user
    if (await userManager.FindByEmailAsync("admin@coffeecare.com") == null)
    {
        var adminUser = new User
        {
            UserName = "admin@coffeecare.com",
            Email = "admin@coffeecare.com",
            FullName = "Administrator",
            EmailConfirmed = true,
            Role = "Admin"
        };

        var result = await userManager.CreateAsync(adminUser, "Admin123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }

    // Create test user
    if (await userManager.FindByEmailAsync("user@test.com") == null)
    {
        var testUser = new User
        {
            UserName = "user@test.com",
            Email = "user@test.com",
            FullName = "Test User",
            EmailConfirmed = true,
            Role = "User"
        };

        var result = await userManager.CreateAsync(testUser, "Test123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(testUser, "User");
        }
    }

    // ✅ SEED DISEASES/SYMPTOMS DATA
    if (!context.Symptoms.Any())
    {
        var symptoms = new[]
        {
            new Symptom { Name = "Đốm màu nâu", Description = "Vết đốm tròn màu nâu trên lá" },
            new Symptom { Name = "Lá xanh tươi", Description = "Lá khỏe mạnh, màu xanh đậm" },
            new Symptom { Name = "Đường hầm trắng", Description = "Đường vân màu trắng uốn khúc trong lá" },
            new Symptom { Name = "Đốm đen", Description = "Vết đốm đen có viền vàng" },
            new Symptom { Name = "Đốm cam/vàng", Description = "Vết đốm màu cam hoặc vàng ở mặt dưới lá" }
        };

        context.Symptoms.AddRange(symptoms);
        await context.SaveChangesAsync();
    }

    // ✅ SEED MODEL VERSIONS
    if (!context.ModelVersions.Any())
    {
        var modelVersions = new[]
        {
            new ModelVersion
            {
                ModelName = "coffee_resnet50_model_final",
                Version = "v1.0",
                FilePath = "/models/coffee_resnet50_model_final.h5",
                Accuracy = 0.92m,
                Notes = "Initial ResNet50 model"
            },
            new ModelVersion
            {
                ModelName = "coffee_resnet50_model_final",
                Version = "v1.1",
                FilePath = "/models/coffee_resnet50_model_final.onnx",
                Accuracy = 0.94m,
                Notes = "ONNX optimized version"
            }
        };

        context.ModelVersions.AddRange(modelVersions);
        await context.SaveChangesAsync();
    }

    Console.WriteLine("✅ Seed data completed");
}