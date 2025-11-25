using DtekMonitor.Models;
using DtekMonitor.Services;
using DtekMonitor.Settings;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DtekMonitor;

/// <summary>
/// Background worker that monitors DTEK schedule and notifies subscribers of changes
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly DtekScraper _scraper;
    private readonly NotificationService _notificationService;
    private readonly ScraperSettings _settings;

    private DtekScheduleData? _lastState;
    private string? _lastStateHash;

    public Worker(
        ILogger<Worker> logger,
        DtekScraper scraper,
        NotificationService notificationService,
        IOptions<ScraperSettings> settings)
    {
        _logger = logger;
        _scraper = scraper;
        _notificationService = notificationService;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker starting...");

        // Initialize scraper
        try
        {
            await _scraper.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize scraper. Worker will retry on first fetch.");
        }

        // Initial fetch
        await FetchAndProcessAsync(stoppingToken);

        // Main monitoring loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_settings.CheckIntervalSeconds * 1000, stoppingToken);
                await FetchAndProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker loop");
                
                // Wait before retrying
                try
                {
                    await Task.Delay(30000, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Worker stopping...");
    }

    private async Task FetchAndProcessAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching schedule data...");

        var newData = await _scraper.FetchDataAsync(cancellationToken);

        if (newData is null)
        {
            _logger.LogWarning("Failed to fetch schedule data");
            return;
        }

        // Calculate hash of new data for comparison
        var newStateHash = ComputeDataHash(newData);

        if (_lastState is null)
        {
            // First fetch - just store the state
            _logger.LogInformation("Initial data fetched. Update time: {UpdateTime}", newData.Update);
            _lastState = newData;
            _lastStateHash = newStateHash;
            return;
        }

        // Check if data changed
        if (newStateHash == _lastStateHash)
        {
            _logger.LogDebug("No changes detected");
            return;
        }

        _logger.LogInformation("Schedule changed! Old update: {OldUpdate}, New update: {NewUpdate}",
            _lastState.Update, newData.Update);

        // Notify subscribers
        try
        {
            await _notificationService.NotifyChangesAsync(_lastState, newData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notifications");
        }

        // Update state
        _lastState = newData;
        _lastStateHash = newStateHash;
    }

    /// <summary>
    /// Computes a hash of the schedule data for quick comparison
    /// </summary>
    private static string ComputeDataHash(DtekScheduleData data)
    {
        // Serialize data part only (excluding update time which changes frequently)
        var json = JsonConvert.SerializeObject(data.Data);
        
        // Simple hash using string hash code (sufficient for change detection)
        return json.GetHashCode().ToString();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker stopping, disposing scraper...");
        await _scraper.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
