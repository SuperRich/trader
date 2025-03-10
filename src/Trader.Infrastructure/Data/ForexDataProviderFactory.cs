using Microsoft.Extensions.DependencyInjection;
using Trader.Core.Services;

namespace Trader.Infrastructure.Data;

/// <summary>
/// Factory for creating forex data providers based on the specified provider type.
/// </summary>
public class ForexDataProviderFactory : IForexDataProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ForexDataProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets a data provider based on the specified provider type.
    /// </summary>
    /// <param name="providerType">The type of provider to use.</param>
    /// <returns>An instance of the specified provider type.</returns>
    /// <exception cref="ArgumentException">Thrown when an invalid provider type is specified.</exception>
    public IForexDataProvider GetProvider(DataProviderType providerType)
    {
        return providerType switch
        {
            DataProviderType.Polygon => _serviceProvider.GetRequiredService<PolygonDataProvider>(),
            DataProviderType.TraderMade => _serviceProvider.GetRequiredService<TraderMadeDataProvider>(),
            DataProviderType.Mock => _serviceProvider.GetRequiredService<ForexDataProvider>(),
            _ => throw new ArgumentException($"Unsupported provider type: {providerType}")
        };
    }
} 