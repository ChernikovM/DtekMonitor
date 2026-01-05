# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["DtekMonitor.csproj", "./"]
RUN dotnet restore "DtekMonitor.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "DtekMonitor.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "DtekMonitor.csproj" -c Release -o /app/publish /p:UseAppHost=false

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
