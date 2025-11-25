using Newtonsoft.Json;

namespace DtekMonitor.Models;

/// <summary>
/// Root model for DTEK schedule data extracted from DisconSchedule.fact
/// </summary>
public class DtekScheduleData
{
    /// <summary>
    /// Dictionary where key is Unix timestamp (string) of the day,
    /// value is dictionary of groups with their hourly statuses
    /// </summary>
    [JsonProperty("data")]
    public Dictionary<string, Dictionary<string, Dictionary<string, string>>> Data { get; set; } = new();

    /// <summary>
    /// Last update time in format "dd.MM.yyyy HH:mm"
    /// </summary>
    [JsonProperty("update")]
    public string Update { get; set; } = string.Empty;

    /// <summary>
    /// Unix timestamp of today
    /// </summary>
    [JsonProperty("today")]
    public long Today { get; set; }
}

/// <summary>
/// Status values for power availability
/// </summary>
public static class PowerStatus
{
    public const string Yes = "yes";
    public const string No = "no";
    public const string First = "first";   // Partial - first half of hour
    public const string Second = "second"; // Partial - second half of hour

    public static string ToDisplayString(string status) => status switch
    {
        Yes => "✅ Світло є",
        No => "🔴 Світла НЕМАЄ",
        First => "⚠️ Частково (перша половина)",
        Second => "⚠️ Частково (друга половина)",
        _ => $"❓ {status}"
    };

    public static string ToShortDisplayString(string status) => status switch
    {
        Yes => "✅",
        No => "🔴",
        First => "⚠️½",
        Second => "½⚠️",
        _ => "❓"
    };
}

/// <summary>
/// Available DTEK groups
/// </summary>
public static class DtekGroups
{
    public static readonly string[] AllGroups =
    [
        "GPV1.1", "GPV1.2",
        "GPV2.1", "GPV2.2",
        "GPV3.1", "GPV3.2",
        "GPV4.1", "GPV4.2",
        "GPV5.1", "GPV5.2",
        "GPV6.1", "GPV6.2"
    ];

    public static bool IsValidGroup(string group)
    {
        return AllGroups.Contains(group.ToUpperInvariant());
    }

    public static string Normalize(string group)
    {
        return group.ToUpperInvariant();
    }
}


