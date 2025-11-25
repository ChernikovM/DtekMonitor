using DtekMonitor;
using DtekMonitor.Commands;
using DtekMonitor.Database;
using DtekMonitor.Services;
using DtekMonitor.Settings;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<TelegramSettings>(
    builder.Configuration.GetSection(TelegramSettings.SectionName));
builder.Services.Configure<ScraperSettings>(
    builder.Configuration.GetSection(ScraperSettings.SectionName));

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register command handlers
CommandHandlerRegistry.RegisterAllHandlers(builder.Services);

// Services (Singletons that manage their own lifecycle)
builder.Services.AddSingleton<DtekScraper>();

// Callback query handler (scoped - needs DbContext)
builder.Services.AddScoped<CallbackQueryHandler>();

// BotService needs to be registered as Singleton first, then as HostedService
// This allows other services (like NotificationService) to inject it
builder.Services.AddSingleton<BotService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<BotService>());

// NotificationService depends on BotService, so register after
builder.Services.AddSingleton<NotificationService>();

// Worker as background service
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Ensure database is created
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    try
    {
        await dbContext.Database.EnsureCreatedAsync();
        Console.WriteLine("Database initialized successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not initialize database: {ex.Message}");
        Console.WriteLine("The application will continue, but database operations may fail.");
    }
}

await host.RunAsync();
