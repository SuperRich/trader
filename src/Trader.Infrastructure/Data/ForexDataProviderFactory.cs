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
        try
        {
            // First try to get the specific provider
            IForexDataProvider? provider = providerType switch
            {
                DataProviderType.Polygon => _serviceProvider.GetService<PolygonDataProvider>(),
                DataProviderType.TraderMade => _serviceProvider.GetService<TraderMadeDataProvider>(),
                DataProviderType.TwelveData => _serviceProvider.GetService<TwelveDataProvider>(),
                DataProviderType.Mock => _serviceProvider.GetService<ForexDataProvider>(),
                _ => throw new ArgumentException($"Unsupported provider type: {providerType}")
            };

            // If the specific provider exists, return it
            if (provider != null)
            {
                return provider;
            }

            // If the specific provider doesn't exist, try to get the default IForexDataProvider
            var defaultProvider = _serviceProvider.GetService<IForexDataProvider>();
            if (defaultProvider != null)
            {
                return defaultProvider;
            }

            // If no provider is available, fall back to mock provider
            return _serviceProvider.GetRequiredService<ForexDataProvider>();
        }
        catch (Exception)
        {
            // Last resort fallback to mock provider
            return _serviceProvider.GetRequiredService<ForexDataProvider>();
        }
    }
}