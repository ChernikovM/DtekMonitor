# === Build Stage ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

ARG GITHUB_TOKEN
ENV GITHUB_TOKEN=${GITHUB_TOKEN}

COPY ["NuGet.config", "./"]
COPY ["DtekMonitor.csproj", "./"]

RUN dotnet restore "DtekMonitor.csproj" --configfile NuGet.config

# --- FIX: КЕШИРОВАНИЕ БРАУЗЕРОВ ---
# 1. Ставим тулзу Playwright глобально
RUN dotnet tool install --global Microsoft.Playwright.CLI
ENV PATH="$PATH:/root/.dotnet/tools"
# 2. Качаем браузер СЕЙЧАС (в /root/.cache). 
# Так как это происходит ДО 'COPY . .', этот слой закешируется навсегда.
RUN playwright install chromium

# --- СБОРКА КОДА ---
COPY . .
RUN dotnet publish "DtekMonitor.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# === Runtime Stage (Production) ===
# ВАЖНО: Используем aspnet:9.0, чтобы точно запустить ваш .NET 9 код
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Устанавливаем зависимости для Linux (обязательно для Playwright)
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

# --- FIX: КОПИРУЕМ БРАУЗЕРЫ ---
# Забираем скачанные браузеры из стадии build. Они уже там есть.
COPY --from=build /root/.cache/ms-playwright /root/.cache/ms-playwright

ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5001

ENTRYPOINT ["dotnet", "DtekMonitor.dll"]