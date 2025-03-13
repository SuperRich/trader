using System;

namespace Trader.Core.Exceptions;

public class TraderException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }

    public TraderException(string message, string errorCode, int statusCode = 500) 
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}

public class ExternalApiException : TraderException
{
    public ExternalApiException(string message, string provider) 
        : base(message, $"EXTERNAL_API_ERROR_{provider.ToUpper()}", 502)
    {
    }
}

public class ValidationException : TraderException
{
    public ValidationException(string message) 
        : base(message, "VALIDATION_ERROR", 400)
    {
    }
}

public class RateLimitException : TraderException
{
    public RateLimitException(string message, string provider) 
        : base(message, $"RATE_LIMIT_{provider.ToUpper()}", 429)
    {
    }
}

public class DataNotFoundException : TraderException
{
    public DataNotFoundException(string message) 
        : base(message, "DATA_NOT_FOUND", 404)
    {
    }
}

public class ConfigurationException : TraderException
{
    public ConfigurationException(string message) 
        : base(message, "CONFIGURATION_ERROR", 500)
    {
    }
} 