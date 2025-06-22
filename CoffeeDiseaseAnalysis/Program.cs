// File: CoffeeDiseaseAnalysis/Program.cs - UPDATED FOR REAL AI
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Data.Entities;
using CoffeeDiseaseAnalysis.Services;
using CoffeeDiseaseAnalysis.Services.Interfaces;

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

// ⚡ REAL AI SERVICE - NO MOCK!
builder.Services.AddScoped<IPredictionService, RealPredictionService>();

// Controllers
builder.Services.AddControllers();

// API Explorer for Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Coffee Disease Analysis API",
        Version = "v2.1-RealAI",
        Description = "🤖 API với REAL AI Model - Không có Mock - Kết quả thật 100%"
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new()
    {
        Description = "JWT Authorization header using the Bearer scheme.",
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

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee Disease Analysis API v2.1 - REAL AI");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Coffee Disease Analysis - REAL AI API";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Serve uploaded images and models

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var predictionService = scope.ServiceProvider.GetRequiredService<IPredictionService>();

    await SeedDataAsync(context, userManager, roleManager, predictionService);
}

app.Run();

// Seed method with model check
static async Task SeedDataAsync(ApplicationDbContext context, UserManager<User> userManager, RoleManager<IdentityRole> roleManager, IPredictionService predictionService)
{
    // Create database if not exists
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

        await userManager.CreateAsync(adminUser, "Admin123!");
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }

    // Create expert user
    if (await userManager.FindByEmailAsync("expert@coffeecare.com") == null)
    {
        var expertUser = new User
        {
            UserName = "expert@coffeecare.com",
            Email = "expert@coffeecare.com",
            FullName = "Coffee Expert",
            EmailConfirmed = true,
            Role = "Expert"
        };

        await userManager.CreateAsync(expertUser, "Expert123!");
        await userManager.AddToRoleAsync(expertUser, "Expert");
    }

    // ⚡ CHECK REAL AI MODEL AVAILABILITY
    var isModelAvailable = await predictionService.IsModelAvailableAsync();
    var modelStats = await predictionService.GetModelStatsAsync();

    Console.WriteLine("====================================");
    Console.WriteLine("🤖 REAL AI MODEL STATUS:");
    Console.WriteLine($"✅ Model Available: {isModelAvailable}");
    Console.WriteLine($"📊 Model Type: {modelStats.ModelType}");
    Console.WriteLine($"📁 Model Version: {modelStats.Version}");

    if (!isModelAvailable)
    {
        Console.WriteLine("❌ WARNING: AI Model not found!");
        Console.WriteLine("📁 Please place 'coffee_resnet50_model_final.onnx' in 'wwwroot/models/' folder");
        Console.WriteLine("🔗 Model file should be converted from .h5 to .onnx format");
    }
    else
    {
        Console.WriteLine("✅ REAL AI Model loaded successfully!");
        Console.WriteLine("🚀 Ready for real disease prediction!");
    }
    Console.WriteLine("====================================");
}
