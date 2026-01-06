# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

ARG GITHUB_TOKEN
ENV GITHUB_TOKEN=${GITHUB_TOKEN}

COPY ["NuGet.config", "./"]
COPY ["DtekMonitor.csproj", "./"]

# 1. Скачиваем пакеты (в том числе Playwright 1.57.0)
RUN dotnet restore "DtekMonitor.csproj" --configfile NuGet.config

# 2. === ГЛАВНЫЙ ФИКС КЕША ===
# Мы не ждем билда. Мы берем скрипт прямо из скачанного NuGet пакета.
# Так как это происходит ДО "COPY . .", браузеры закешируются навсегда (пока не сменишь версию пакета).
RUN pwsh /root/.nuget/packages/microsoft.playwright/1.57.0/build/playwright.ps1 install chromium

# 3. Теперь копируем код и билдим
COPY . .
RUN dotnet build "DtekMonitor.csproj" -c Release -o /app/build --no-restore

# Publish
FROM build AS publish
RUN dotnet publish "DtekMonitor.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Твои системные зависимости (оставляем как было, это правильно)
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

ENV TZ=Europe/Kiev
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Копируем само приложение
COPY --from=publish /app/publish .

# 4. Копируем БРАУЗЕРЫ из стадии build (они лежат в папке root)
COPY --from=build /root/.cache/ms-playwright /root/.cache/ms-playwright

ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

ENTRYPOINT ["dotnet", "DtekMonitor.dll"]