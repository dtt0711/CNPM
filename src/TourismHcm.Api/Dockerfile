# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY TourismHcm.slnx ./
COPY src/TourismHcm.Domain/TourismHcm.Domain.csproj src/TourismHcm.Domain/
COPY src/TourismHcm.Contracts/TourismHcm.Contracts.csproj src/TourismHcm.Contracts/
COPY src/TourismHcm.Application/TourismHcm.Application.csproj src/TourismHcm.Application/
COPY src/TourismHcm.Infrastructure/TourismHcm.Infrastructure.csproj src/TourismHcm.Infrastructure/
COPY src/TourismHcm.Api/TourismHcm.Api.csproj src/TourismHcm.Api/

RUN dotnet restore src/TourismHcm.Api/TourismHcm.Api.csproj

COPY src/ src/
RUN dotnet publish src/TourismHcm.Api/TourismHcm.Api.csproj -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends wget \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 10001 appgroup \
    && useradd --uid 10001 --gid appgroup --create-home --shell /usr/sbin/nologin appuser \
    && mkdir -p /app/audio-storage \
    && chown -R appuser:appgroup /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

COPY --from=build --chown=appuser:appgroup /app/publish .

USER appuser
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD wget -qO- http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "TourismHcm.Api.dll"]
