﻿namespace CountryCurrencyApi.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public Dictionary<string, string>? Errors { get; set; }

        public static ApiResponse<T> SuccessResponse(T data, string message = "")
        {
            return new ApiResponse<T> { Success = true, Data = data, Message = message };
        }

        public static ApiResponse<T> ErrorResponse(string message, Dictionary<string, string>? errors = null)
        {
            return new ApiResponse<T> { Success = false, Message = message, Errors = errors };
        }
    }
}