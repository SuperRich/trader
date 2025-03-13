using System;

namespace Trader.Core.Models;

public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public object? Details { get; set; }

    public ErrorResponse(string message, string errorCode, string requestId, object? details = null)
    {
        Message = message;
        ErrorCode = errorCode;
        Timestamp = DateTime.UtcNow;
        RequestId = requestId;
        Details = details;
    }
} 