FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["Trader.Api.csproj", "./"]
RUN dotnet restore "Trader.Api.csproj"

# Copy everything else and build
COPY . .
RUN dotnet publish "Trader.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user for security
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && adduser --disabled-password --gecos "" appuser \
    && chown -R appuser /app

# Copy published app
COPY --from=build /app/publish .
RUN chmod +x /app

# Create health check endpoint
RUN mkdir -p /app/health && echo "OK" > /app/health/index.html

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 80

# Set entry point
ENTRYPOINT ["dotnet", "Trader.Api.dll"]

# Add health check endpoint
HEALTHCHECK --interval=30s --timeout=30s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:80/health || exit 1
