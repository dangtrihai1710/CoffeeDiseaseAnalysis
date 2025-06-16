using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CoffeeDiseaseAnalysis.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "User"),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Symptoms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Weight = table.Column<decimal>(type: "decimal(3,2)", nullable: false, defaultValue: 1.0m),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Symptoms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeafImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UploadDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ImageStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    ImageHash = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FileExtension = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeafImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeafImages_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Accuracy = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    ValidationAccuracy = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    TestAccuracy = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsProduction = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TrainingDatasetVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TrainingSamples = table.Column<int>(type: "int", nullable: false),
                    ValidationSamples = table.Column<int>(type: "int", nullable: false),
                    TestSamples = table.Column<int>(type: "int", nullable: false),
                    ModelType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "CNN"),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DeployedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FileChecksum = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelVersions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LeafImageSymptoms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeafImageId = table.Column<int>(type: "int", nullable: false),
                    SymptomId = table.Column<int>(type: "int", nullable: false),
                    ObservedDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Intensity = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ObservedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeafImageSymptoms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeafImageSymptoms_AspNetUsers_ObservedByUserId",
                        column: x => x.ObservedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeafImageSymptoms_LeafImages_LeafImageId",
                        column: x => x.LeafImageId,
                        principalTable: "LeafImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeafImageSymptoms_Symptoms_SymptomId",
                        column: x => x.SymptomId,
                        principalTable: "Symptoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PredictionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeafImageId = table.Column<int>(type: "int", nullable: false),
                    ModelType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApiStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ModelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProcessingTimeMs = table.Column<int>(type: "int", nullable: false),
                    ServerNode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PredictionLogs_LeafImages_LeafImageId",
                        column: x => x.LeafImageId,
                        principalTable: "LeafImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeafImageId = table.Column<int>(type: "int", nullable: false),
                    DiseaseName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PredictionDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ModelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TreatmentSuggestion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SeverityLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Unknown"),
                    FinalConfidence = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    ProcessingTimeMs = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Predictions_LeafImages_LeafImageId",
                        column: x => x.LeafImageId,
                        principalTable: "LeafImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Feedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PredictionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FeedbackText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    FeedbackDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CorrectDiseaseName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsUsedForTraining = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    FeedbackType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Manual")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Feedbacks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Feedbacks_Predictions_PredictionId",
                        column: x => x.PredictionId,
                        principalTable: "Predictions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainingDataRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeafImageId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IsValidated = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DatasetSplit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "train"),
                    IsUsedForTraining = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OriginalPrediction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OriginalConfidence = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    ValidatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FeedbackId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Quality = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Unknown")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingDataRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingDataRecords_AspNetUsers_ValidatedByUserId",
                        column: x => x.ValidatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TrainingDataRecords_Feedbacks_FeedbackId",
                        column: x => x.FeedbackId,
                        principalTable: "Feedbacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TrainingDataRecords_LeafImages_LeafImageId",
                        column: x => x.LeafImageId,
                        principalTable: "LeafImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ModelVersions",
                columns: new[] { "Id", "Accuracy", "CreatedAt", "CreatedByUserId", "DeployedAt", "FileChecksum", "FilePath", "FileSizeBytes", "ModelName", "ModelType", "Notes", "TestAccuracy", "TestSamples", "TrainingDatasetVersion", "TrainingSamples", "ValidationAccuracy", "ValidationSamples", "Version" },
                values: new object[] { 1, 0.8500m, new DateTime(2023, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "/models/coffee_resnet50_v1.0.h5", 265281000L, "coffee_resnet50", "CNN", "Mô hình ResNet50 ban đầu - baseline model", 0.8100m, 400, "v1.0", 2000, 0.8200m, 400, "v1.0" });

            migrationBuilder.InsertData(
                table: "ModelVersions",
                columns: new[] { "Id", "Accuracy", "CreatedAt", "CreatedByUserId", "DeployedAt", "FileChecksum", "FilePath", "FileSizeBytes", "IsActive", "IsProduction", "ModelName", "ModelType", "Notes", "TestAccuracy", "TestSamples", "TrainingDatasetVersion", "TrainingSamples", "ValidationAccuracy", "ValidationSamples", "Version" },
                values: new object[] { 2, 0.8750m, new DateTime(2023, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateTime(2023, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, "/models/coffee_resnet50_v1.1.onnx", 120000000L, true, true, "coffee_resnet50", "CNN", "Cải tiến với data augmentation, fine-tuning và chuyển đổi sang ONNX", 0.8400m, 500, "v1.1", 2500, 0.8500m, 500, "v1.1" });

            migrationBuilder.InsertData(
                table: "ModelVersions",
                columns: new[] { "Id", "Accuracy", "CreatedAt", "CreatedByUserId", "DeployedAt", "FileChecksum", "FilePath", "FileSizeBytes", "IsActive", "ModelName", "ModelType", "Notes", "TestAccuracy", "TestSamples", "TrainingDatasetVersion", "TrainingSamples", "ValidationAccuracy", "ValidationSamples", "Version" },
                values: new object[,]
                {
                    { 3, 0.7200m, new DateTime(2023, 11, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "/models/coffee_mlp_v1.0.onnx", 5000000L, true, "coffee_mlp", "MLP", "MLP cho phân tích triệu chứng - hỗ trợ CNN", 0.6900m, 300, "v1.0", 1500, 0.7000m, 300, "v1.0" },
                    { 4, 0.9100m, new DateTime(2023, 12, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "/models/coffee_combined_v1.0.onnx", 125000000L, true, "coffee_combined", "Combined", "Kết hợp CNN và MLP với trọng số 0.7:0.3", 0.8800m, 500, "v1.1", 2500, 0.8900m, 500, "v1.0" }
                });

            migrationBuilder.InsertData(
                table: "ModelVersions",
                columns: new[] { "Id", "Accuracy", "CreatedAt", "CreatedByUserId", "DeployedAt", "FileChecksum", "FilePath", "FileSizeBytes", "ModelName", "ModelType", "Notes", "TestAccuracy", "TestSamples", "TrainingDatasetVersion", "TrainingSamples", "ValidationAccuracy", "ValidationSamples", "Version" },
                values: new object[] { 5, 0.9200m, new DateTime(2023, 12, 17, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "/models/coffee_resnet50_v2.0.onnx", 118000000L, "coffee_resnet50", "CNN", "Huấn luyện lại với feedback từ người dùng và SMOTE để xử lý dữ liệu không cân bằng", 0.8950m, 600, "v2.0", 3000, 0.9000m, 600, "v2.0" });

            migrationBuilder.InsertData(
                table: "Symptoms",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "IsActive", "Name", "Weight" },
                values: new object[,]
                {
                    { 1, "Leaf", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Các vệt màu nâu xuất hiện trên bề mặt lá", true, "Vệt nâu trên lá", 0.8m },
                    { 2, "Leaf", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Các đốm màu cam đỏ đặc trưng của bệnh rỉ sắt", true, "Vết đốm cam đỏ", 0.9m },
                    { 3, "Leaf", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Lá bị héo, mất độ tươi", true, "Lá héo", 0.7m },
                    { 4, "Leaf", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Lá chuyển màu vàng bất thường", true, "Lá vàng", 0.6m },
                    { 5, "Leaf", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Viền lá chuyển màu nâu", true, "Đường viền lá nâu", 0.7m },
                    { 6, "Leaf", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Các lỗ nhỏ do sâu đục", true, "Lỗ thủng trên lá", 0.8m },
                    { 7, "Leaf", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Bề mặt lá bị khô, nứt nẻ", true, "Bề mặt lá khô", 0.6m },
                    { 8, "Leaf", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Các vệt màu trắng do nấm", true, "Vệt trắng", 0.75m },
                    { 9, "Leaf", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Lá bị cong vặn do sâu bệnh", true, "Lá cong vặn", 0.85m },
                    { 10, "Leaf", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Mép lá bị khô, cháy", true, "Mép lá khô", 0.65m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_FullName",
                table: "AspNetUsers",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_FeedbackDate",
                table: "Feedbacks",
                column: "FeedbackDate");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_FeedbackType",
                table: "Feedbacks",
                column: "FeedbackType");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_IsUsedForTraining",
                table: "Feedbacks",
                column: "IsUsedForTraining");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_PredictionId",
                table: "Feedbacks",
                column: "PredictionId");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_Rating",
                table: "Feedbacks",
                column: "Rating");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_UserId",
                table: "Feedbacks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeafImages_ImageHash",
                table: "LeafImages",
                column: "ImageHash");

            migrationBuilder.CreateIndex(
                name: "IX_LeafImages_ImageStatus",
                table: "LeafImages",
                column: "ImageStatus");

            migrationBuilder.CreateIndex(
                name: "IX_LeafImages_UploadDate",
                table: "LeafImages",
                column: "UploadDate");

            migrationBuilder.CreateIndex(
                name: "IX_LeafImages_UserId",
                table: "LeafImages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeafImageSymptoms_Intensity",
                table: "LeafImageSymptoms",
                column: "Intensity");

            migrationBuilder.CreateIndex(
                name: "IX_LeafImageSymptoms_LeafImageId_SymptomId",
                table: "LeafImageSymptoms",
                columns: new[] { "LeafImageId", "SymptomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeafImageSymptoms_ObservedByUserId",
                table: "LeafImageSymptoms",
                column: "ObservedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeafImageSymptoms_ObservedDate",
                table: "LeafImageSymptoms",
                column: "ObservedDate");

            migrationBuilder.CreateIndex(
                name: "IX_LeafImageSymptoms_SymptomId",
                table: "LeafImageSymptoms",
                column: "SymptomId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_CreatedAt",
                table: "ModelVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_CreatedByUserId",
                table: "ModelVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_IsActive",
                table: "ModelVersions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_IsProduction",
                table: "ModelVersions",
                column: "IsProduction");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_ModelName_Version",
                table: "ModelVersions",
                columns: new[] { "ModelName", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_ModelType",
                table: "ModelVersions",
                column: "ModelType");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionLogs_ApiStatus",
                table: "PredictionLogs",
                column: "ApiStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionLogs_LeafImageId",
                table: "PredictionLogs",
                column: "LeafImageId");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionLogs_ModelType",
                table: "PredictionLogs",
                column: "ModelType");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionLogs_ModelVersion",
                table: "PredictionLogs",
                column: "ModelVersion");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionLogs_RequestId",
                table: "PredictionLogs",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionLogs_RequestTime",
                table: "PredictionLogs",
                column: "RequestTime");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_DiseaseName",
                table: "Predictions",
                column: "DiseaseName");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_LeafImageId_ModelVersion",
                table: "Predictions",
                columns: new[] { "LeafImageId", "ModelVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_ModelVersion",
                table: "Predictions",
                column: "ModelVersion");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_PredictionDate",
                table: "Predictions",
                column: "PredictionDate");

            migrationBuilder.CreateIndex(
                name: "IX_Symptoms_Category",
                table: "Symptoms",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Symptoms_IsActive",
                table: "Symptoms",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Symptoms_Name",
                table: "Symptoms",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingDataRecords_DatasetSplit",
                table: "TrainingDataRecords",
                column: "DatasetSplit");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingDataRecords_FeedbackId",
                table: "TrainingDataRecords",
                column: "FeedbackId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingDataRecords_IsUsedForTraining",
                table: "TrainingDataRecords",
                column: "IsUsedForTraining");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingDataRecords_IsValidated",
                table: "TrainingDataRecords",
                column: "IsValidated");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingDataRecords_Label",
                table: "TrainingDataRecords",
                column: "Label");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingDataRecords_LeafImageId",
                table: "TrainingDataRecords",
                column: "LeafImageId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingDataRecords_Quality",
                table: "TrainingDataRecords",
                column: "Quality");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingDataRecords_Source",
                table: "TrainingDataRecords",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingDataRecords_ValidatedByUserId",
                table: "TrainingDataRecords",
                column: "ValidatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "LeafImageSymptoms");

            migrationBuilder.DropTable(
                name: "ModelVersions");

            migrationBuilder.DropTable(
                name: "PredictionLogs");

            migrationBuilder.DropTable(
                name: "TrainingDataRecords");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Symptoms");

            migrationBuilder.DropTable(
                name: "Feedbacks");

            migrationBuilder.DropTable(
                name: "Predictions");

            migrationBuilder.DropTable(
                name: "LeafImages");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
