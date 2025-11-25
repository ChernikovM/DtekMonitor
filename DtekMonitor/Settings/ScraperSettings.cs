namespace DtekMonitor.Settings;

public class ScraperSettings
{
    public const string SectionName = "Scraper";
    
    public string TargetUrl { get; set; } = "https://www.dtek-krem.com.ua/ua/shutdowns";
    public int WaitTimeSeconds { get; set; } = 12;
    public int CheckIntervalSeconds { get; set; } = 60;
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    public int ViewportWidth { get; set; } = 1920;
    public int ViewportHeight { get; set; } = 1080;
    public string Locale { get; set; } = "uk-UA";
    public string TimezoneId { get; set; } = "Europe/Kiev";
}


