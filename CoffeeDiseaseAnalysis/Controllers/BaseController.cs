// ===================================================================
// 5. File: CoffeeDiseaseAnalysis/Controllers/BaseController.cs
// ===================================================================
using CoffeeDiseaseAnalysis.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeDiseaseAnalysis.Controllers
{
    [ApiController]
    public abstract class BaseController : ControllerBase
    {
        protected ActionResult<ApiResponse<T>> Success<T>(T data, string message = "Success")
        {
            return Ok(new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                StatusCode = 200
            });
        }

        protected ActionResult<ApiResponse<object>> Success(string message = "Success")
        {
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = message,
                StatusCode = 200
            });
        }

        protected ActionResult<ApiResponse<object>> Error(string message, int statusCode = 400, List<string>? errors = null)
        {
            return StatusCode(statusCode, new ApiResponse<object>
            {
                Success = false,
                Message = message,
                Errors = errors ?? new List<string>(),
                StatusCode = statusCode
            });
        }

        protected ActionResult<ApiResponse<T>> NotFound<T>(string message = "Không tìm thấy dữ liệu")
        {
            return NotFound(new ApiResponse<T>
            {
                Success = false,
                Message = message,
                StatusCode = 404
            });
        }

        protected ActionResult<PaginatedResult<T>> Paginated<T>(List<T> items, int totalCount, int pageNumber, int pageSize)
        {
            return Ok(new PaginatedResult<T>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }
    }
}
