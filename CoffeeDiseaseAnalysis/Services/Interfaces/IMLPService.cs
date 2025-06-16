// File: CoffeeDiseaseAnalysis/Services/Interfaces/IMLPService.cs
using CoffeeDiseaseAnalysis.Data;
using CoffeeDiseaseAnalysis.Models.DTOs;
using CoffeeDiseaseAnalysis.Services.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace CoffeeDiseaseAnalysis.Services.Interfaces
{
    public interface IMLPService
    {
        Task<decimal> PredictFromSymptomsAsync(List<int> symptomIds);
        Task<Dictionary<string, decimal>> PredictAllClassesFromSymptomsAsync(List<int> symptomIds);
        Task TrainMLPModelAsync();
        Task<bool> IsModelAvailableAsync();
    }
}

