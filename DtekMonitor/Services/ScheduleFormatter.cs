using System.Text;
using DtekMonitor.Models;

namespace DtekMonitor.Services;

/// <summary>
/// Helper service for formatting schedule data for display
/// </summary>
public static class ScheduleFormatter
{
    /// <summary>
    /// Formats schedule for a specific day
    /// </summary>
    public static string FormatDaySchedule(
        Dictionary<string, string> groupData,
        string groupName,
        string dayLabel,
        DateTime dateTime,
        bool showCurrentHourMarker = false)
    {
        var sb = new StringBuilder();
        
        // Show display name (e.g., "3.2" instead of "GPV3.2")
        var displayName = DtekGroups.ToDisplayName(groupName);
        sb.AppendLine($"📊 <b>Черга {displayName}</b> | {dayLabel}");
        sb.AppendLine($"📅 {dateTime:dd.MM.yyyy}");
        sb.AppendLine();

        // In DTEK data: key "1" = 00:00-01:00, key "2" = 01:00-02:00, etc.
        // Current hour marker: if it's 14:30, we're in period "15" (14:00-15:00)
        var currentHourKey = showCurrentHourMarker ? DateTime.Now.Hour + 1 : -1;

        for (int hour = 1; hour <= 24; hour++)
        {
            var hourKey = hour.ToString();
            var status = groupData.TryGetValue(hourKey, out var s) ? s : "?";
            var statusIcon = PowerStatus.ToShortDisplayString(status);

            // Key "1" = 00:00-01:00, Key "2" = 01:00-02:00, etc.
            var startHour = (hour - 1).ToString("D2");  // hour 1 -> "00"
            var endHour = hour == 24 ? "00" : hour.ToString("D2");  // hour 24 -> "00" (next day)

            var marker = hour == currentHourKey ? "👉 " : "   ";

            sb.AppendLine($"{marker}<code>{startHour}-{endHour}</code> {statusIcon}");
        }

        sb.AppendLine();
        sb.AppendLine("<b>Легенда:</b> ✅ є | 🔴 немає | ⚠️½ частк.");

        return sb.ToString();
    }

    /// <summary>
    /// Gets timestamp for tomorrow based on today's timestamp
    /// </summary>
    public static long GetTomorrowTimestamp(long todayTimestamp)
    {
        return todayTimestamp + 86400; // Add 24 hours in seconds
    }

    /// <summary>
    /// Converts Unix timestamp to DateTime
    /// </summary>
    public static DateTime TimestampToDateTime(long timestamp)
    {
        return DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
    }

    /// <summary>
    /// Checks if tomorrow's schedule is available
    /// </summary>
    public static bool IsTomorrowAvailable(DtekScheduleData data)
    {
        var tomorrowTimestamp = GetTomorrowTimestamp(data.Today).ToString();
        return data.Data.ContainsKey(tomorrowTimestamp);
    }

    /// <summary>
    /// Gets available days from schedule data
    /// </summary>
    public static List<string> GetAvailableDays(DtekScheduleData data)
    {
        return data.Data.Keys.OrderBy(k => k).ToList();
    }
}

