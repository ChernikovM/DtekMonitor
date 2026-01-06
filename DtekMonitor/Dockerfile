# === Build Stage ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

ARG GITHUB_TOKEN
ENV GITHUB_TOKEN=${GITHUB_TOKEN}

COPY ["NuGet.config", "./"]
COPY ["DtekMonitor.csproj", "./"]

RUN dotnet restore "DtekMonitor.csproj" --configfile NuGet.config

# --- FIX: КЕШИРОВАНИЕ БРАУЗЕРОВ ---
RUN dotnet tool install --global Microsoft.Playwright.CLI
ENV PATH="$PATH:/root/.dotnet/tools"
# Качаем браузеры в слой build
RUN playwright install chromium

# --- СБОРКА КОДА ---
COPY . .
RUN dotnet publish "DtekMonitor.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# === Runtime Stage (Production) ===
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Устанавливаем зависимости для Linux
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

# --- ИСПРАВЛЕНИЕ ЗДЕСЬ ---
# Было --from=publish (ошибка), стало --from=build (правильно)
COPY --from=build /app/publish .

# Копируем браузеры из стадии build
COPY --from=build /root/.cache/ms-playwright /root/.cache/ms-playwright

ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5001

ENTRYPOINT ["dotnet", "DtekMonitor.dll"]