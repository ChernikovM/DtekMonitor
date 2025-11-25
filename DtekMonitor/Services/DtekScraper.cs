using System.Text.RegularExpressions;
using DtekMonitor.Models;
using DtekMonitor.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Newtonsoft.Json;

namespace DtekMonitor.Services;

/// <summary>
/// Service for scraping DTEK power outage schedules using Playwright
/// </summary>
public class DtekScraper : IAsyncDisposable
{
    private readonly ILogger<DtekScraper> _logger;
    private readonly ScraperSettings _settings;
    
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;
    
    // Regex pattern to extract DisconSchedule.fact JSON from HTML
    private static readonly Regex ScheduleRegex = new(
        @"DisconSchedule\.fact\s*=\s*(\{.*?\});",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // Store last fetched data for quick access
    private DtekScheduleData? _lastData;
    private readonly object _dataLock = new();

    public DtekScraper(
        ILogger<DtekScraper> logger,
        IOptions<ScraperSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    /// <summary>
    /// Gets the last successfully fetched schedule data
    /// </summary>
    public DtekScheduleData? GetLastData()
    {
        lock (_dataLock)
        {
            return _lastData;
        }
    }

    /// <summary>
    /// Initializes Playwright and browser instance
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            _logger.LogInformation("Initializing Playwright...");
            
            _playwright = await Playwright.CreateAsync();
            
            // Minimal args - matching the working Node.js script
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args =
                [
                    "--disable-blink-features=AutomationControlled",
                    "--no-sandbox",
                    "--disable-setuid-sandbox"
                ]
            });

            _isInitialized = true;
            _logger.LogInformation("Playwright initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Playwright");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Fetches the current schedule data from DTEK website
    /// </summary>
    public async Task<DtekScheduleData?> FetchDataAsync(CancellationToken cancellationToken = default)
    {
        if (!_isInitialized || _browser is null)
        {
            await InitializeAsync();
        }

        IBrowserContext? context = null;
        IPage? page = null;

        try
        {
            _logger.LogDebug("Creating browser context...");

            context = await _browser!.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = _settings.UserAgent,
                ViewportSize = new ViewportSize
                {
                    Width = _settings.ViewportWidth,
                    Height = _settings.ViewportHeight
                },
                Locale = _settings.Locale,
                TimezoneId = _settings.TimezoneId,
                JavaScriptEnabled = true
            });

            page = await context.NewPageAsync();

            _logger.LogDebug("Navigating to {Url}", _settings.TargetUrl);

            // Navigate WITHOUT WaitUntil option - matching the working Node.js script
            _logger.LogDebug("Navigating to {Url}...", _settings.TargetUrl);
            await page.GotoAsync(_settings.TargetUrl, new PageGotoOptions
            {
                Timeout = 60000
            });

            // Use Playwright's WaitForTimeoutAsync instead of Task.Delay
            _logger.LogDebug("Waiting {Seconds}s for page to load...", _settings.WaitTimeSeconds);
            await page.WaitForTimeoutAsync(_settings.WaitTimeSeconds * 1000);

            // Try to get DisconSchedule.fact directly from JavaScript context
            // This is more reliable than parsing HTML
            string? jsonFromJs = null;
            try
            {
                jsonFromJs = await page.EvaluateAsync<string?>(
                    "() => typeof DisconSchedule !== 'undefined' && DisconSchedule.fact ? JSON.stringify(DisconSchedule.fact) : null");
                
                if (jsonFromJs != null)
                {
                    _logger.LogDebug("Got DisconSchedule.fact from JS context ({Length} chars)", jsonFromJs.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Could not get DisconSchedule.fact from JS: {Message}", ex.Message);
            }

            // If we got data from JS, use it directly
            if (!string.IsNullOrEmpty(jsonFromJs))
            {
                var jsData = JsonConvert.DeserializeObject<DtekScheduleData>(jsonFromJs);
                if (jsData != null)
                {
                    _logger.LogInformation("Successfully fetched schedule data from JS. Update time: {UpdateTime}", jsData.Update);
                    lock (_dataLock)
                    {
                        _lastData = jsData;
                    }
                    return jsData;
                }
            }

            // Fallback: Get page content and parse with regex
            var content = await page.ContentAsync();
            _logger.LogDebug("Page content length: {Length}", content.Length);

            // Extract JSON from DisconSchedule.fact
            var match = ScheduleRegex.Match(content);

            if (!match.Success)
            {
                _logger.LogWarning("Failed to find DisconSchedule.fact in page content (length: {Length})", content.Length);
                
                // Log more content for debugging - check if it's WAF page
                var preview = content.Length > 2000 ? content[..2000] : content;
                _logger.LogWarning("Page content preview: {Preview}", preview);
                
                // Check for common WAF indicators
                if (content.Contains("Incapsula") || content.Contains("_Incapsula"))
                {
                    _logger.LogWarning("Detected Incapsula WAF challenge page - need more wait time or different approach");
                }
                
                if (content.Contains("challenge") || content.Contains("captcha"))
                {
                    _logger.LogWarning("Detected challenge/captcha in response");
                }
                
                return null;
            }

            var jsonString = match.Groups[1].Value;
            _logger.LogDebug("Extracted JSON ({Length} chars)", jsonString.Length);

            // Deserialize using Newtonsoft.Json (handles non-strict JSON better)
            var data = JsonConvert.DeserializeObject<DtekScheduleData>(jsonString);

            if (data is null)
            {
                _logger.LogWarning("Failed to deserialize schedule data");
                return null;
            }

            _logger.LogInformation("Successfully fetched schedule data. Update time: {UpdateTime}", data.Update);

            // Store last data
            lock (_dataLock)
            {
                _lastData = data;
            }

            return data;
        }
        catch (PlaywrightException ex)
        {
            _logger.LogError(ex, "Playwright error while fetching data");
            
            // Try to restart browser on critical errors
            await RestartBrowserAsync();
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching schedule data");
            return null;
        }
        finally
        {
            if (page is not null)
            {
                await page.CloseAsync();
            }
            
            if (context is not null)
            {
                await context.CloseAsync();
            }
        }
    }

    /// <summary>
    /// Restarts the browser instance after an error
    /// </summary>
    private async Task RestartBrowserAsync()
    {
        _logger.LogWarning("Restarting browser...");

        await _initLock.WaitAsync();
        try
        {
            if (_browser is not null)
            {
                try
                {
                    await _browser.CloseAsync();
                }
                catch
                {
                    // Ignore close errors
                }
            }

            _isInitialized = false;
            _browser = null;

            // Re-initialize with same minimal args
            if (_playwright is not null)
            {
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Args =
                    [
                        "--disable-blink-features=AutomationControlled",
                        "--no-sandbox",
                        "--disable-setuid-sandbox"
                    ]
                });
                _isInitialized = true;
                _logger.LogInformation("Browser restarted successfully");
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
        
        _initLock.Dispose();
        
        GC.SuppressFinalize(this);
    }
}


