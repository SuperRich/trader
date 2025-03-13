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
                        "https://localhost:7001"  // Backend development server (HTTPS)
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        return services;
    }
} 