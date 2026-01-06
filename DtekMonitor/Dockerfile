# === Build Stage ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

ARG GITHUB_TOKEN
ENV GITHUB_TOKEN=${GITHUB_TOKEN}

# 1. Задаем путь для браузеров ЯВНО, чтобы знать, где они лежат
ENV PLAYWRIGHT_BROWSERS_PATH=/pw-browsers

COPY ["NuGet.config", "./"]
COPY ["DtekMonitor.csproj", "./"]

# 2. Восстанавливаем пакеты (качается Playwright 1.57.0)
RUN dotnet restore "DtekMonitor.csproj" --configfile NuGet.config

# 3. Устанавливаем браузеры в нашу папку /pw-browsers
# Мы делаем это ДО копирования кода (COPY . .), чтобы слой закешировался.
RUN pwsh /root/.nuget/packages/microsoft.playwright/1.57.0/build/playwright.ps1 install chromium

# 4. Копируем код и билдим
COPY . .
RUN dotnet build "DtekMonitor.csproj" -c Release -o /app/build --no-restore

# Publish
FROM build AS publish
RUN dotnet publish "DtekMonitor.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# === Runtime Stage ===
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Важные системные зависимости
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

# Копируем приложение
COPY --from=publish /app/publish .

# 5. ГЛАВНОЕ ИСПРАВЛЕНИЕ:
# Указываем приложению, где искать браузеры
ENV PLAYWRIGHT_BROWSERS_PATH=/pw-browsers
# Копируем папку с браузерами из стадии build
COPY --from=build /pw-browsers /pw-browsers

ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000

ENTRYPOINT ["dotnet", "DtekMonitor.dll"]