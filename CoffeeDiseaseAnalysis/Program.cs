// File: CoffeeDiseaseAnalysis/Program.cs - FIXED AUTHENTICATION
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

// Add services to the container
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ JWT AUTHENTICATION CONFIGURATION - FIXED
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

    // ✅ CUSTOM CHALLENGE RESPONSE - KHÔNG REDIRECT VỀ LOGIN PAGE
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.HandleResponse();

            // ✅ TRẢ VỀ JSON THAY VÌ REDIRECT
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            var response = new
            {
                success = false,
                message = "Token không hợp lệ hoặc hết hạn",
                statusCode = 401,
                errors = new[] { "Vui lòng đăng nhập lại" }
            };

            return context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        },
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"❌ JWT Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"✅ JWT Token validated for: {context.Principal?.Identity?.Name}");
            return Task.CompletedTask;
        }
    };
});

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

// ✅ AUTHORIZATION
builder.Services.AddAuthorization();

// ✅ CORS - CHO PHÉP FRONTEND CONNECT
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

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
        Version = "v2.1-Fixed",
        Description = "🤖 API phân tích bệnh lá cà phê với AI - Authentication Fixed"
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Coffee Disease Analysis API v1");
        c.RoutePrefix = "swagger";
    });
}

// ✅ MIDDLEWARE ORDER - QUAN TRỌNG!
app.UseHttpsRedirection();

// ✅ CORS - PHẢI TRƯỚC AUTHENTICATION
app.UseCors("AllowFrontend");

// ✅ STATIC FILES
app.UseStaticFiles();

// ✅ ROUTING
app.UseRouting();

// ✅ AUTHENTICATION & AUTHORIZATION - SAU CORS, TRƯỚC CONTROLLERS
app.UseAuthentication();
app.UseAuthorization();

// ✅ CONTROLLERS
app.MapControllers();

// ✅ RUN MIGRATIONS AUTOMATICALLY
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
    }
}

Console.WriteLine("🚀 Coffee Disease Analysis API Started");
Console.WriteLine("📊 Swagger UI: https://localhost:7179/swagger");
Console.WriteLine("🔗 API Base: https://localhost:7179/api");

app.Run();