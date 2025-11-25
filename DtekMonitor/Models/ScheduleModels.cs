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
/// Available DTEK groups (queues)
/// </summary>
public static class DtekGroups
{
    /// <summary>
    /// Display names for users (matching DTEK website format: "Черга X.Y")
    /// </summary>
    public static readonly string[] DisplayGroups =
    [
        "1.1", "1.2",
        "2.1", "2.2",
        "3.1", "3.2",
        "4.1", "4.2",
        "5.1", "5.2",
        "6.1", "6.2"
    ];

    /// <summary>
    /// Internal API names used in DTEK data (GPV prefix)
    /// </summary>
    public static readonly string[] ApiGroups =
    [
        "GPV1.1", "GPV1.2",
        "GPV2.1", "GPV2.2",
        "GPV3.1", "GPV3.2",
        "GPV4.1", "GPV4.2",
        "GPV5.1", "GPV5.2",
        "GPV6.1", "GPV6.2"
    ];

    /// <summary>
    /// Converts display name (1.1) to API name (GPV1.1)
    /// </summary>
    public static string ToApiName(string displayName)
    {
        var normalized = displayName.Trim().ToUpperInvariant();
        
        // If already has GPV prefix, return as is
        if (normalized.StartsWith("GPV"))
            return normalized;
        
        return $"GPV{normalized}";
    }

    /// <summary>
    /// Converts API name (GPV1.1) to display name (1.1)
    /// </summary>
    public static string ToDisplayName(string apiName)
    {
        var normalized = apiName.Trim().ToUpperInvariant();
        
        if (normalized.StartsWith("GPV"))
            return normalized[3..]; // Remove "GPV" prefix
        
        return normalized;
    }

    public static bool IsValidGroup(string group)
    {
        var normalized = group.Trim().ToUpperInvariant();
        
        // Check both formats
        if (normalized.StartsWith("GPV"))
            return ApiGroups.Contains(normalized);
        
        return DisplayGroups.Contains(normalized);
    }

    /// <summary>
    /// Normalizes any input to API format (GPV prefix)
    /// </summary>
    public static string Normalize(string group)
    {
        return ToApiName(group);
    }
}


