// File: CoffeeDiseaseAnalysis/Data/DatabaseInitializer.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data.Entities;

namespace CoffeeDiseaseAnalysis.Data
{
    public static class DatabaseInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();

            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                var userManager = services.GetRequiredService<UserManager<User>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                // Tạo database nếu chưa tồn tại
                await context.Database.EnsureCreatedAsync();

                // Chạy migrations
                if (context.Database.GetPendingMigrations().Any())
                {
                    await context.Database.MigrateAsync();
                    logger.LogInformation("Database migrations applied successfully");
                }

                // Seed roles
                await SeedRolesAsync(roleManager, logger);

                // Seed admin user
                await SeedAdminUserAsync(userManager, logger);

                logger.LogInformation("Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while initializing the database");
                throw;
            }
        }

        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
        {
            string[] roles = { "Admin", "Expert", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                    logger.LogInformation("Created role: {Role}", role);
                }
            }
        }

        private static async Task SeedAdminUserAsync(UserManager<User> userManager, ILogger logger)
        {
            const string adminEmail = "admin@coffeedisease.com";
            const string adminPassword = "Admin123!";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new User
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmailConfirmed = true,
                    Role = "Admin",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    logger.LogInformation("Created admin user: {Email}", adminEmail);
                }
                else
                {
                    logger.LogError("Failed to create admin user: {Errors}",
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}