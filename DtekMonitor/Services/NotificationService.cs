using System.Text;
using DtekMonitor.Database;
using DtekMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DtekMonitor.Services;

/// <summary>
/// Service for building and sending schedule change notifications
/// </summary>
public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BotService _botService;

    public NotificationService(
        ILogger<NotificationService> logger,
        IServiceScopeFactory scopeFactory,
        BotService botService)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _botService = botService;
    }

    /// <summary>
    /// Compares old and new schedule data and sends notifications for changes
    /// </summary>
    public async Task NotifyChangesAsync(
        DtekScheduleData oldData,
        DtekScheduleData newData,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get all subscribers grouped by their group
        var subscribers = await dbContext.Subscribers
            .ToListAsync(cancellationToken);

        if (subscribers.Count == 0)
        {
            _logger.LogDebug("No subscribers to notify");
            return;
        }

        var subscribersByGroup = subscribers
            .GroupBy(s => s.GroupName)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Compare data for each group that has subscribers
        foreach (var (groupName, groupSubscribers) in subscribersByGroup)
        {
            var changes = GetGroupChanges(oldData, newData, groupName);

            if (changes.Count == 0)
                continue;

            var message = BuildChangeNotification(groupName, changes, newData.Update);

            _logger.LogInformation("Sending notification to {Count} subscribers of group {Group}",
                groupSubscribers.Count, groupName);

            foreach (var subscriber in groupSubscribers)
            {
                await _botService.SendMessageAsync(subscriber.ChatId, message, cancellationToken);
                
                // Small delay to avoid rate limiting
                await Task.Delay(50, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Gets the changes for a specific group between old and new data
    /// </summary>
    private List<ScheduleChange> GetGroupChanges(
        DtekScheduleData oldData,
        DtekScheduleData newData,
        string groupName)
    {
        var changes = new List<ScheduleChange>();

        // Check today's data
        var todayTimestamp = newData.Today.ToString();

        if (!newData.Data.TryGetValue(todayTimestamp, out var newTodayData))
            return changes;

        if (!newTodayData.TryGetValue(groupName, out var newGroupData))
            return changes;

        // Get old data for comparison
        Dictionary<string, string>? oldGroupData = null;
        if (oldData.Data.TryGetValue(todayTimestamp, out var oldTodayData))
        {
            oldTodayData.TryGetValue(groupName, out oldGroupData);
        }

        // Compare each hour
        for (int hour = 1; hour <= 24; hour++)
        {
            var hourKey = hour.ToString();
            
            var newStatus = newGroupData.TryGetValue(hourKey, out var ns) ? ns : null;
            var oldStatus = oldGroupData?.TryGetValue(hourKey, out var os) == true ? os : null;

            if (newStatus != oldStatus && newStatus is not null)
            {
                changes.Add(new ScheduleChange
                {
                    Hour = hour,
                    OldStatus = oldStatus,
                    NewStatus = newStatus
                });
            }
        }

        return changes;
    }

    /// <summary>
    /// Builds a notification message for schedule changes
    /// </summary>
    private string BuildChangeNotification(
        string groupName,
        List<ScheduleChange> changes,
        string updateTime)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"🔔 <b>Зміни в графіку для групи {groupName}</b>");
        sb.AppendLine($"🕐 Оновлено: {updateTime}");
        sb.AppendLine();

        foreach (var change in changes.OrderBy(c => c.Hour))
        {
            var hourDisplay = change.Hour == 24 ? "00" : change.Hour.ToString("D2");
            var nextHour = change.Hour == 24 ? "01" : (change.Hour + 1).ToString("D2");
            
            var oldStatusText = change.OldStatus is not null 
                ? PowerStatus.ToDisplayString(change.OldStatus) 
                : "❓ Невідомо";
            var newStatusText = PowerStatus.ToDisplayString(change.NewStatus);

            sb.AppendLine($"<code>{hourDisplay}:00-{nextHour}:00</code>");
            sb.AppendLine($"   {oldStatusText} → {newStatusText}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Represents a change in schedule for a specific hour
    /// </summary>
    private class ScheduleChange
    {
        public int Hour { get; init; }
        public string? OldStatus { get; init; }
        public string NewStatus { get; init; } = string.Empty;
    }
}


