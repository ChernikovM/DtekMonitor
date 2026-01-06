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
RUN dotnet restore "DtekMonitor.csproj" --configfile NuGet.config

# Copy everything else and build
COPY . .
RUN dotnet build "DtekMonitor.csproj" -c Release -o /app/build --no-restore

# Publish
FROM build AS publish
RUN dotnet publish "DtekMonitor.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Install Playwright browsers in publish stage
WORKDIR /app/publish
RUN pwsh playwright.ps1 install chromium
RUN pwsh playwright.ps1 install-deps chromium

# Runtime stage - .NET 9.0 (Debian-based)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install Playwright system dependencies
RUN apt-get update && apt-get install -y \
    libnss3 \
    libnspr4 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libdbus-1-3 \
    libxkbcommon0 \
    libatspi2.0-0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libasound2 \
    libpango-1.0-0 \
    libcairo2 \
    libx11-xcb1 \
    && rm -rf /var/lib/apt/lists/*

# Set timezone
ENV TZ=Europe/Kiev
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Copy published app with Playwright browsers
COPY --from=publish /app/publish .
COPY --from=publish /root/.cache/ms-playwright /root/.cache/ms-playwright

# Set environment variables
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port for Management API
EXPOSE 5000

ENTRYPOINT ["dotnet", "DtekMonitor.dll"]
