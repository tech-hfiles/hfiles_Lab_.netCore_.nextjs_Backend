using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HFiles_Backend.Application.DTOs.Labs;

namespace HFiles_Backend.Application.Common
{
    public static class ApiResponseFactory
    {
        public static ApiResponse<T> Success<T>(T data, string? message = null)
        {
            return new ApiResponse<T> { Success = true, Data = data, Message = message };
        }

        public static ApiResponse<object?> Success(string message)
        {
            return new ApiResponse<object?> { Success = true, Data = null, Message = message };
        }

        public static ApiResponse<T> PartialSuccess<T>(T data, string? message = null)
        {
            return new ApiResponse<T> { Success = true, Data = data, Message = message };
        }

        public static ApiResponse<T> Fail<T>(string message)
        {
            return new ApiResponse<T> { Success = false, Data = default, Message = message };
        }

        public static ApiResponse<object?> Fail(string message)
        {
            return new ApiResponse<object?> { Success = false, Data = null, Message = message };
        }

        public static ApiResponse<List<string>> Fail(List<string> errors)
        {
            return new ApiResponse<List<string>> { Success = false, Data = errors, Message = "Validation failed." };
        }

        public static ApiResponse<T> Fail<T>(T data, string? message = null)
        {
            return new ApiResponse<T> { Success = false, Data = data, Message = message };
        }
    }
}
