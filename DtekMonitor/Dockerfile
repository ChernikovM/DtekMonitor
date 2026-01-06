# === Stage 1: Build ===
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

ARG GITHUB_TOKEN
ENV GITHUB_TOKEN=${GITHUB_TOKEN}

COPY ["NuGet.config", "./"]
COPY ["DtekMonitor.csproj", "./"]

RUN dotnet restore "DtekMonitor.csproj" --configfile NuGet.config

COPY . .
# Билдим проект
RUN dotnet publish "DtekMonitor.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# === Stage 2: Runtime (Production) ===
# Используем официальный образ Playwright, соответствующий вашему пакету (1.57.0).
# В нем УЖЕ ЕСТЬ все браузеры и системные зависимости.
FROM mcr.microsoft.com/playwright/dotnet:v1.57.0-jammy AS final
WORKDIR /app

# В этом образе по умолчанию .NET 8. 
# Мы доустанавливаем Runtime .NET 9 одной командой.
RUN apt-get update && \
    apt-get install -y aspnetcore-runtime-9.0 && \
    rm -rf /var/lib/apt/lists/*

ENV TZ=Europe/Kiev
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Копируем наше приложение из стадии build
COPY --from=build /app/publish .

ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production

# Порт приложения (убедитесь, что в docker-compose проброс совпадает)
EXPOSE 5000

ENTRYPOINT ["dotnet", "DtekMonitor.dll"]