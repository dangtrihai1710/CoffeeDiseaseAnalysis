// File: CoffeeDiseaseAnalysis/Data/DatabaseSeeder.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoffeeDiseaseAnalysis.Data.Entities;

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

                // Seed Demo Users
                await SeedDemoUsersAsync(userManager, logger);

                // Seed Additional Symptoms (if needed)
                await SeedAdditionalSymptomsAsync(context, logger);

                // Seed Additional Model Versions (if needed)
                await SeedAdditionalModelsAsync(context, logger);

                await context.SaveChangesAsync();
                logger.LogInformation("✅ Database seeding completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error occurred during database seeding");
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
                    var result = await roleManager.CreateAsync(new IdentityRole(role));
                    if (result.Succeeded)
                    {
                        logger.LogInformation("✅ Created role: {Role}", role);
                    }
                    else
                    {
                        logger.LogError("❌ Failed to create role: {Role}. Errors: {Errors}",
                            role, string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
            }
        }

        private static async Task SeedDemoUsersAsync(UserManager<User> userManager, ILogger logger)
        {
            var demoUsers = new[]
            {
                new { Email = "admin@coffeedisease.com", Password = "Admin123!", FullName = "System Administrator", Role = "Admin" },
                new { Email = "expert@coffeedisease.com", Password = "Expert123!", FullName = "Disease Expert", Role = "Expert" },
                new { Email = "user@demo.com", Password = "User123!", FullName = "Demo User", Role = "User" }
            };

            foreach (var userData in demoUsers)
            {
                var existingUser = await userManager.FindByEmailAsync(userData.Email);
                if (existingUser == null)
                {
                    var user = new User
                    {
                        UserName = userData.Email,
                        Email = userData.Email,
                        FullName = userData.FullName,
                        EmailConfirmed = true,
                        Role = userData.Role,
                        CreatedAt = DateTime.UtcNow
                    };

                    var result = await userManager.CreateAsync(user, userData.Password);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, userData.Role);
                        logger.LogInformation("✅ Created demo user: {Email} with role {Role}",
                            userData.Email, userData.Role);
                    }
                    else
                    {
                        logger.LogError("❌ Failed to create demo user: {Email}. Errors: {Errors}",
                            userData.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
            }
        }

        private static async Task SeedAdditionalSymptomsAsync(ApplicationDbContext context, ILogger logger)
        {
            // Kiểm tra xem đã có symptoms chưa
            var existingSymptoms = await context.Symptoms.CountAsync();
            if (existingSymptoms >= 10)
            {
                logger.LogInformation("Symptoms already seeded, skipping...");
                return;
            }

            // Thêm symptoms bổ sung nếu cần
            var additionalSymptoms = new List<Symptom>
            {
                new Symptom { Name = "Đốm vàng nhạt", Description = "Các đốm màu vàng nhạt xuất hiện trên lá", Category = "Leaf", Weight = 0.7m },
                new Symptom { Name = "Lá co rút", Description = "Lá bị co rút, biến dạng", Category = "Leaf", Weight = 0.8m },
                new Symptom { Name = "Mủ trắng", Description = "Xuất hiện chất mủ màu trắng", Category = "Leaf", Weight = 0.9m },
                new Symptom { Name = "Gân lá đỏ", Description = "Gân lá chuyển màu đỏ bất thường", Category = "Leaf", Weight = 0.75m },
                new Symptom { Name = "Lá dày bất thường", Description = "Lá dày hơn bình thường", Category = "Leaf", Weight = 0.6m }
            };

            foreach (var symptom in additionalSymptoms)
            {
                var exists = await context.Symptoms.AnyAsync(s => s.Name == symptom.Name);
                if (!exists)
                {
                    context.Symptoms.Add(symptom);
                }
            }
        }

        private static async Task SeedAdditionalModelsAsync(ApplicationDbContext context, ILogger logger)
        {
            // Kiểm tra xem đã có model versions chưa
            var existingModels = await context.ModelVersions.CountAsync();
            if (existingModels >= 5)
            {
                logger.LogInformation("Model versions already seeded, skipping...");
                return;
            }

            // Thêm model version mới nhất
            var latestModel = new ModelVersion
            {
                ModelName = "coffee_resnet50",
                Version = "v2.1",
                FilePath = "/models/coffee_resnet50_v2.1.onnx",
                Accuracy = 0.9300m,
                ValidationAccuracy = 0.9150m,
                TestAccuracy = 0.9050m,
                Notes = "Phiên bản mới nhất với cải tiến từ feedback người dùng và data augmentation nâng cao",
                IsActive = false,
                IsProduction = false,
                TrainingDatasetVersion = "v2.1",
                TrainingSamples = 3500,
                ValidationSamples = 700,
                TestSamples = 700,
                ModelType = "CNN",
                FileSizeBytes = 115000000,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            };

            var exists = await context.ModelVersions.AnyAsync(m => m.Version == latestModel.Version);
            if (!exists)
            {
                context.ModelVersions.Add(latestModel);
            }
        }
    }
}