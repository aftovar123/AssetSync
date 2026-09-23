FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/AssetSync.Domain/AssetSync.Domain.csproj src/AssetSync.Domain/
COPY src/AssetSync.Application/AssetSync.Application.csproj src/AssetSync.Application/
COPY src/AssetSync.Infrastructure/AssetSync.Infrastructure.csproj src/AssetSync.Infrastructure/
COPY src/AssetSync.Api/AssetSync.Api.csproj src/AssetSync.Api/
RUN dotnet restore src/AssetSync.Api/AssetSync.Api.csproj

COPY src/ src/
RUN dotnet publish src/AssetSync.Api/AssetSync.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AssetSync.Api.dll"]
