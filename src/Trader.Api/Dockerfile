FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Trader.Api/Trader.Api.csproj", "src/Trader.Api/"]
COPY ["src/Trader.Core/Trader.Core.csproj", "src/Trader.Core/"]
COPY ["src/Trader.Infrastructure/Trader.Infrastructure.csproj", "src/Trader.Infrastructure/"]
RUN dotnet restore "src/Trader.Api/Trader.Api.csproj"
COPY . .
WORKDIR "/src/src/Trader.Api"
RUN dotnet build "Trader.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Trader.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Trader.Api.dll"]
