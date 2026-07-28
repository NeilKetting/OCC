using System;
using System.Collections.Generic;

namespace OCC.Shared.Framework
{
    /// <summary>
    /// Universal standard API response wrapper across the OCC platform.
    /// </summary>
    /// <typeparam name="T">Payload data type.</typeparam>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string TraceId { get; set; } = Guid.NewGuid().ToString("N");

        public static ApiResponse<T> Ok(T data, string message = "Request processed successfully.")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null)
        {
            var response = new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = default
            };

            if (errors != null)
            {
                response.Errors.AddRange(errors);
            }

            return response;
        }
    }
}
