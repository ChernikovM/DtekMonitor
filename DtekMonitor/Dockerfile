# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# GitHub token for private NuGet packages (passed as build arg)
ARG GITHUB_TOKEN
ENV GITHUB_TOKEN=${GITHUB_TOKEN}

# Copy NuGet.config and csproj first for better layer caching
COPY ["NuGet.config", "./"]
COPY ["DtekMonitor.csproj", "./"]

# Restore with GitHub Packages authentication
# The token is substituted from environment variable in NuGet.config
RUN dotnet restore "DtekMonitor.csproj" --configfile NuGet.config

# Copy everything else and build
COPY . .
RUN dotnet build "DtekMonitor.csproj" -c Release -o /app/build --no-restore

# Publish
FROM build AS publish
RUN dotnet publish "DtekMonitor.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage - using Playwright base image with all required dependencies
FROM mcr.microsoft.com/playwright/dotnet:v1.48.0-jammy AS final
WORKDIR /app

# Set timezone
ENV TZ=Europe/Kiev
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Copy published app
COPY --from=publish /app/publish .

# Set environment variables
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port for Management API (Swagger, Analytics)
EXPOSE 5000

ENTRYPOINT ["dotnet", "DtekMonitor.dll"]
