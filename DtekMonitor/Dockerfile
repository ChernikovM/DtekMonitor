# === Stage 1: Build ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

ARG GITHUB_TOKEN
ENV GITHUB_TOKEN=${GITHUB_TOKEN}

COPY ["NuGet.config", "./"]
COPY ["DtekMonitor.csproj", "./"]

RUN dotnet restore "DtekMonitor.csproj" --configfile NuGet.config

COPY . .
RUN dotnet publish "DtekMonitor.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# === Stage 2: Runtime (Clean & Cached) ===
# Используем официальный образ Playwright. 
# ВАЖНО: Замените 'v1.49.0' на версию вашего NuGet пакета Microsoft.Playwright!
# Этот образ уже содержит: .NET SDK/Runtime, Chromium, WebKit, Firefox и все системные либы.
FROM mcr.microsoft.com/playwright/dotnet:v1.48.0 AS final

WORKDIR /app

# Настраиваем часовой пояс
ENV TZ=Europe/Kiev
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Копируем сбилженное приложение
COPY --from=build /app/publish .

ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production

# Порт Management API
EXPOSE 5000

ENTRYPOINT ["dotnet", "DtekMonitor.dll"]