// 6. APPLICATION SERVICES - ENHANCED AI SERVICES WITH ADVANCED IMAGE PROCESSING
using CoffeeDiseaseAnalysis.Services;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using CoffeeDiseaseAnalysis.Services.Mock;

Console.WriteLine("🤖 Registering ENHANCED AI Services with advanced image processing...");

// Core Services (luôn có)
builder.Services.AddScoped<ICacheService, CacheService>();

// Check multiple possible model paths
var possibleModelPaths = new[]
{
        Path.Combine(builder.Environment.WebRootPath ?? "wwwroot", "models", "coffee_resnet50_v1.1.onnx"),
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "models", "coffee_resnet50_v1.1.onnx"),
        Path.Combine(builder.Environment.ContentRootPath, "models", "coffee_resnet50_v1.1.onnx"),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "models", "coffee_resnet50_v1.1.onnx"),
        Path.Combine(Directory.GetCurrentDirectory(), "models", "coffee_resnet50_v1.1.onnx")
    };

string? foundModelPath = null;
foreach (var modelPath in possibleModelPaths)
{
    Console.WriteLine($"🔍 Checking enhanced model path: {modelPath}");
    if (File.Exists(modelPath))
    {
        foundModelPath = modelPath;
        Console.WriteLine($"✅ Enhanced model found at: {modelPath}");
        break;
    }
    else
    {
        Console.WriteLine($"❌ Enhanced model not found at: {modelPath}");
    }
}

// Force UseRealAI with Enhanced Processing
var useEnhancedAI = true; // FORCE TO TRUE FOR ENHANCED PROCESSING
var modelExists = foundModelPath != null;

Console.WriteLine($"📊 Enhanced model exists: {modelExists}");
Console.WriteLine($"⚙️ UseEnhancedAI setting: {useEnhancedAI} (FORCED)");
Console.WriteLine($"📁 Found enhanced model path: {foundModelPath ?? "NONE"}");

if (useEnhancedAI && modelExists)
{
    Console.WriteLine("✅ Using ENHANCED AI Services with advanced image processing");
    Console.WriteLine("🔬 Features enabled:");
    Console.WriteLine("   • Advanced image quality analysis");
    Console.WriteLine("   • Coffee leaf feature extraction");
    Console.WriteLine("   • Environmental factor detection");
    Console.WriteLine("   • Adaptive image enhancement");
    Console.WriteLine("   • Quality-based confidence adjustment");

    // ENHANCED Real AI Services
    builder.Services.AddScoped<IPredictionService, EnhancedRealPredictionService>();
    builder.Services.AddScoped<IMLPService, MockMLPService>(); // MLP vẫn dùng mock
    builder.Services.AddScoped<IMessageQueueService, MockMessageQueueService>();
}
else
{
    if (!modelExists)
    {
        Console.WriteLine("⚠️ Enhanced model file not found in any expected location!");
        Console.WriteLine("📋 Please ensure coffee_resnet50_v1.1.onnx exists in /wwwroot/models/");
        Console.WriteLine("📋 Current working directory: " + Directory.GetCurrentDirectory());
        Console.WriteLine("📋 WebRootPath: " + (builder.Environment.WebRootPath ?? "NULL"));
        Console.WriteLine("📋 ContentRootPath: " + builder.Environment.ContentRootPath);
    }

    Console.WriteLine("📋 Falling back to Mock Services");

    // Mock Services fallback
    builder.Services.AddScoped<IPredictionService, MockPredictionService>();
    builder.Services.AddScoped<IMLPService, MockMLPService>();
    builder.Services.AddScoped<IMessageQueueService, MockMessageQueueService>();
}