// File: CoffeeDiseaseAnalysis/Data/DatabaseSeeder.cs
using CoffeeDiseaseAnalysis.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoffeeDiseaseAnalysis.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(
            ApplicationDbContext context,
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger logger)
        {
            try
            {
                // Seed Roles
                await SeedRolesAsync(roleManager, logger);

                // Seed Users
                await SeedUsersAsync(userManager, logger);

                // Seed Symptoms
                await SeedSymptomsAsync(context, logger);

                // Seed Model Versions
                await SeedModelVersionsAsync(context, logger);

                logger.LogInformation("✅ Database seeding completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error occurred while seeding database");
                throw;
            }
        }

        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
        {
            var roles = new[] { "Admin", "Expert", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                    logger.LogInformation("Created role: {Role}", role);
                }
            }
        }

        private static async Task SeedUsersAsync(UserManager<User> userManager, ILogger logger)
        {
            // Admin User
            var adminEmail = "admin@coffeedisease.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new User
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "System Administrator",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(admin, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                    logger.LogInformation("Created admin user: {Email}", adminEmail);
                }
            }

            // Expert User
            var expertEmail = "expert@coffeedisease.com";
            if (await userManager.FindByEmailAsync(expertEmail) == null)
            {
                var expert = new User
                {
                    UserName = expertEmail,
                    Email = expertEmail,
                    EmailConfirmed = true,
                    FullName = "Coffee Disease Expert",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(expert, "Expert123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(expert, "Expert");
                    logger.LogInformation("Created expert user: {Email}", expertEmail);
                }
            }

            // Demo User
            var userEmail = "user@demo.com";
            if (await userManager.FindByEmailAsync(userEmail) == null)
            {
                var user = new User
                {
                    UserName = userEmail,
                    Email = userEmail,
                    EmailConfirmed = true,
                    FullName = "Demo User",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user, "User123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "User");
                    logger.LogInformation("Created demo user: {Email}", userEmail);
                }
            }
        }

        private static async Task SeedSymptomsAsync(ApplicationDbContext context, ILogger logger)
        {
            if (!await context.Symptoms.AnyAsync())
            {
                var symptoms = new[]
                {
                    new Symptom { Name = "Đốm nâu trên lá", Description = "Các đốm tròn màu nâu xuất hiện trên bề mặt lá", Category = "Leaf", Weight = 0.8m },
                    new Symptom { Name = "Vàng lá", Description = "Lá chuyển màu vàng bất thường", Category = "Leaf", Weight = 0.6m },
                    new Symptom { Name = "Héo lá", Description = "Lá bị héo, khô", Category = "Leaf", Weight = 0.7m },
                    new Symptom { Name = "Đốm cam dưới lá", Description = "Các đốm màu cam xuất hiện ở mặt dưới lá", Category = "Leaf", Weight = 0.9m },
                    new Symptom { Name = "Lỗ thủng trên lá", Description = "Các lỗ nhỏ trên bề mặt lá do sâu đục", Category = "Leaf", Weight = 0.8m },
                    new Symptom { Name = "Đốm đen", Description = "Các vết đốm màu đen trên lá", Category = "Leaf", Weight = 0.7m },
                    new Symptom { Name = "Cạnh lá cháy", Description = "Cạnh lá bị cháy, khô", Category = "Leaf", Weight = 0.5m },
                    new Symptom { Name = "Lá xoăn", Description = "Lá bị xoăn, biến dạng", Category = "Leaf", Weight = 0.6m }
                };

                context.Symptoms.AddRange(symptoms);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} symptoms", symptoms.Length);
            }
        }

        private static async Task SeedModelVersionsAsync(ApplicationDbContext context, ILogger logger)
        {
            if (!await context.ModelVersions.AnyAsync())
            {
                var modelVersions = new[]
                {
                    new ModelVersion
                    {
                        ModelName = "coffee_resnet50",
                        Version = "v1.1",
                        FilePath = "/models/coffee_resnet50_v1.1.onnx",
                        Accuracy = 0.92m,
                        ValidationAccuracy = 0.89m,
                        TestAccuracy = 0.87m,
                        TrainingDatasetVersion = "dataset_v1.0",
                        TrainingSamples = 5000,
                        ValidationSamples = 1000,
                        TestSamples = 500,
                        ModelType = "CNN",
                        FileSizeBytes = 25600000, // 25.6MB
                        IsActive = true,
                        IsProduction = true,
                        Notes = "ResNet50 model trained on coffee disease dataset"
                    },
                    new ModelVersion
                    {
                        ModelName = "coffee_mlp",
                        Version = "v1.0",
                        FilePath = "/models/coffee_mlp_v1.0.onnx",
                        Accuracy = 0.78m,
                        ValidationAccuracy = 0.76m,
                        TestAccuracy = 0.74m,
                        TrainingDatasetVersion = "symptoms_v1.0",
                        TrainingSamples = 2000,
                        ValidationSamples = 400,
                        TestSamples = 200,
                        ModelType = "MLP",
                        FileSizeBytes = 5000000, // 5MB
                        IsActive = true,
                        IsProduction = false,
                        Notes = "MLP model for symptom-based prediction"
                    }
                };

                context.ModelVersions.AddRange(modelVersions);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} model versions", modelVersions.Length);
            }
        }
    }
}