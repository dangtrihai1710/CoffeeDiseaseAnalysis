// File: CoffeeDiseaseAnalysis/Program.cs - SIMPLIFIED CONFIGURATION
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

// Add services
builder.Services.AddScoped<IPredictionService, SimplePredictionService>();

// Controllers
builder.Services.AddControllers();

// API Explorer for Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Coffee Disease Analysis API",
        Version = "v2.0-Simplified",
        Description = "API đơn giản hóa cho phân tích bệnh lá cà phê"
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new()
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
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
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee Disease Analysis API v2.0");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Serve uploaded images

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

    await SeedDataAsync(context, userManager, roleManager);
}

app.Run();

// Seed method
static async Task SeedDataAsync(ApplicationDbContext context, UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
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
}