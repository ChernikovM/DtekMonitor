# === Stage 1: Build ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

ARG GITHUB_TOKEN
ENV GITHUB_TOKEN=${GITHUB_TOKEN}

# Copy project files and restore
COPY ["NuGet.config", "./"]
COPY ["DtekMonitor.csproj", "./"]
RUN dotnet restore "DtekMonitor.csproj" --configfile NuGet.config

# Copy everything and publish
COPY . .
RUN dotnet publish "DtekMonitor.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# === Stage 2: Install Playwright browsers ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS playwright
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Install Playwright CLI and download Chromium
RUN dotnet tool install --global Microsoft.Playwright.CLI --version 1.57.0
ENV PATH="$PATH:/root/.dotnet/tools"
RUN playwright install chromium
RUN playwright install-deps chromium

# === Stage 3: Runtime ===
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install Playwright runtime dependencies for Chromium
RUN apt-get update && apt-get install -y --no-install-recommends \
    libnss3 \
    libnspr4 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libasound2 \
    libpango-1.0-0 \
    libcairo2 \
    && rm -rf /var/lib/apt/lists/*

# Set timezone
ENV TZ=Europe/Kiev
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Copy published app
COPY --from=build /app/publish .

# Copy Playwright browsers from playwright stage
COPY --from=playwright /root/.cache/ms-playwright /root/.cache/ms-playwright

ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "DtekMonitor.dll"]
