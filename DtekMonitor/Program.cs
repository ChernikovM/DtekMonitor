using DtekMonitor;
using DtekMonitor.Database;
using DtekMonitor.Middleware;
using DtekMonitor.Services;
using DtekMonitor.Settings;
using Microsoft.EntityFrameworkCore;
using Spacebar.Bedrock.Configurations.Extensions;
using Spacebar.Bedrock.Cqrs;
using Spacebar.Bedrock.Persistence.EntityFramework;
using Spacebar.Bedrock.Telegram.Core.Configuration;
using Spacebar.Bedrock.Telegram.Core.DependencyInjection;
using Spacebar.Bedrock.Telegram.Core.Features.MessageLogging;
using Spacebar.Bedrock.Telegram.Management.Configuration;
using Spacebar.Bedrock.Telegram.Management.DependencyInjection;
using Spacebar.Bedrock.Telegram.Management.Endpoints;
using Spacebar.Bedrock.Telegram.Management.Swagger;

// Create web application builder (instead of Host.CreateApplicationBuilder)
var builder = WebApplication.CreateBuilder(args);

// ========================================
// 1. Bedrock Configurations
// ========================================
// Auto-loads TelegramBotConfig, TelegramDatabaseConfig from JSON files in Configurations/
builder.AddBedrockConfigurations(typeof(TelegramDatabaseConfig).Assembly);
builder.AddConfiguration<ManagementConfig>();

// Legacy configuration for Scraper (keep existing settings)
builder.Services.Configure<ScraperSettings>(
    builder.Configuration.GetSection(ScraperSettings.SectionName));

// ========================================
// 2. Bedrock CQRS (required for Management API endpoints)
// ========================================
builder.Services.AddBedrockCqrs(
    typeof(Program).Assembly,                     // User's queries (future)
    typeof(ManagementBuilder).Assembly            // SDK built-in queries
);

// ========================================
// 3. Bedrock Persistence (EF Core)
// ========================================
builder.Services.AddBedrockEfPersistence();

// ========================================
// 4. Database (PostgreSQL)
// ========================================
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var dbConfig = serviceProvider.GetRequiredService<TelegramDatabaseConfig>();
    options.UseNpgsql(dbConfig.BuildConnectionString());
});

// ========================================
// 5. Bedrock Telegram Bot with Management API
// ========================================
builder.Services.AddBedrockTelegram()
    .UseDbContext<AppDbContext>()               // Single DbContext registration
    .WithMessageLogging(opts =>                  // Enable message logging feature
    {
        opts.LogMessageContent = true;
        opts.LogCommandsOnly = false;
    })
    .WithManagement(mgmt =>                      // Enable Management API
    {
        mgmt.UseStats();                         // /stats, /stats/users, /stats/messages
        mgmt.UseHealth();                        // /health
        mgmt.UseInfo();                          // /info
        mgmt.UseBroadcast();                     // /broadcast - send messages to all users
    })
    .Use<DtekCallbackMiddleware>()              // Handle inline keyboard callbacks
    .AddCommandHandlers(typeof(Program).Assembly); // Register all command handlers
    // Note: SDK automatically handles button-to-command mapping via AliasResolutionMiddleware
    // when command handlers define Aliases property

// ========================================
// 6. Existing Services (preserved)
// ========================================
// DtekScraper - singleton for fetching schedule data
builder.Services.AddSingleton<DtekScraper>();

// NotificationService - now uses ITelegramBotClient from DI
builder.Services.AddSingleton<NotificationService>();

// Worker - background service for monitoring DTEK schedule
builder.Services.AddHostedService<Worker>();

// ========================================
// Build Application
// ========================================
var app = builder.Build();

// ========================================
// 7. HTTP Pipeline
// ========================================
app.UseAuthentication();
app.UseAuthorization();

// Swagger UI for Management API
app.UseManagementSwagger();

// ========================================
// 8. Map Bedrock CQRS endpoints
// ========================================
app.MapBedrockEndpoints(
    typeof(ManagementBuilder).Assembly           // SDK built-in queries
    // typeof(Program).Assembly
);

// ========================================
// 9. Initialize Database
// ========================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    try
    {
        // Note: In production, use migrations instead of EnsureCreated
        await dbContext.Database.EnsureCreatedAsync();
        Console.WriteLine("Database initialized successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not initialize database: {ex.Message}");
        Console.WriteLine("The application will continue, but database operations may fail.");
    }
}

// ========================================
// 10. Startup Banner
// ========================================
Console.WriteLine();
Console.WriteLine("===========================================");
Console.WriteLine("  DTEK Monitor Bot");
Console.WriteLine("  with Bedrock SDK Management API");
Console.WriteLine("===========================================");
Console.WriteLine();
Console.WriteLine("Bot is starting...");
Console.WriteLine("Management API available at: /api/management/tg/*");
Console.WriteLine("Swagger UI available at: /api/management/tg/swagger");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

await app.RunAsync();
