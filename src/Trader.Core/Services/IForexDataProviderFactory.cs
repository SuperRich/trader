namespace Trader.Core.Services;

/// <summary>
/// Factory interface for creating forex data providers.
/// </summary>
public interface IForexDataProviderFactory
{
    /// <summary>
    /// Gets a data provider based on the specified provider type.
    /// </summary>
    /// <param name="providerType">The type of provider to use.</param>
    /// <returns>An instance of the specified provider type.</returns>
    IForexDataProvider GetProvider(DataProviderType providerType);
}

/// <summary>
/// Enum representing the available data provider types.
/// </summary>
public enum DataProviderType
{
    /// <summary>
    /// Mock data provider that generates simulated data.
    /// </summary>
    Mock,
    
    /// <summary>
    /// Polygon.io data provider for real market data.
    /// </summary>
    Polygon,
    
    /// <summary>
    /// TraderMade data provider for real market data.
    /// </summary>
    TraderMade,
    
    /// <summary>
    /// TwelveData data provider for real market data.
    /// </summary>
    TwelveData
}