using System;

namespace Trader.Api;

public static class CorsConfiguration
{
    public const string DefaultPolicy = "DefaultPolicy";

    public static IServiceCollection ConfigureCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(DefaultPolicy, policy =>
            {
                policy
                    .WithOrigins(
                        "http://localhost:3000",  // Next.js development server (HTTP)
                        "https://localhost:3000", // Next.js development server (HTTPS)
                        "http://localhost:7001",  // Backend development server (HTTP)
                        "https://localhost:7001", // Backend development server (HTTPS)
                        "http://217.154.57.29",   // VPS IP (HTTP)
                        "https://217.154.57.29",  // VPS IP (HTTPS)
                        "http://217.154.57.29:80", // VPS IP with client port (HTTP)
                        "http://217.154.57.29:8080" // VPS IP with API port (HTTP)
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
                
                // For production, we're explicitly listing the allowed origins above
                // rather than allowing all origins, which would be a security risk
            });
        });

        return services;
    }
}
