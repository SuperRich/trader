using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Trader.Core.Exceptions;
using Trader.Core.Models;

namespace Trader.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var requestId = context.TraceIdentifier;
        var path = context.Request.Path;
        
        var (statusCode, errorResponse) = exception switch
        {
            ValidationException ve => (400, 
                new ErrorResponse(ve.Message, "VALIDATION_ERROR", requestId)),
            
            RateLimitException re => (429, 
                new ErrorResponse(re.Message, "RATE_LIMIT_EXCEEDED", requestId)),

            DataNotFoundException nf => (404,
                new ErrorResponse(nf.Message, "DATA_NOT_FOUND", requestId)),
            
            ConfigurationException ce => (500,
                new ErrorResponse(ce.Message, "CONFIGURATION_ERROR", requestId)),
            
            TraderException te => (te.StatusCode, 
                new ErrorResponse(te.Message, te.ErrorCode, requestId)),
            
            // Fallback for unexpected errors
            _ => (500, new ErrorResponse(
                "An unexpected error occurred. Please try again later.",
                "INTERNAL_SERVER_ERROR",
                requestId))
        };

        // Log the error with context
        _logger.LogError(exception, 
            "Error processing request {RequestId}. Path: {Path}. Error: {Error}",
            requestId, path, exception.Message);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        
        await context.Response.WriteAsJsonAsync(errorResponse);
    }
} 