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

        // Check if tomorrow's schedule just appeared
        var oldHasTomorrow = ScheduleFormatter.IsTomorrowAvailable(oldData);
        var newHasTomorrow = ScheduleFormatter.IsTomorrowAvailable(newData);
        var tomorrowJustAppeared = !oldHasTomorrow && newHasTomorrow;

        if (tomorrowJustAppeared)
        {
            _logger.LogInformation("Tomorrow's schedule just appeared! Notifying all subscribers.");
        }

        // Compare data for each group that has subscribers
        foreach (var (groupName, groupSubscribers) in subscribersByGroup)
        {
            var todayChanges = GetGroupChanges(oldData, newData, groupName, newData.Today);
            var tomorrowChanges = tomorrowJustAppeared 
                ? GetGroupChanges(oldData, newData, groupName, ScheduleFormatter.GetTomorrowTimestamp(newData.Today))
                : new List<ScheduleChange>();

            // Notify about tomorrow's schedule appearing
            if (tomorrowJustAppeared)
            {
                var tomorrowMessage = BuildTomorrowAppearedNotification(groupName, newData);
                
                _logger.LogInformation("Sending tomorrow notification to {Count} subscribers of group {Group}",
                    groupSubscribers.Count, groupName);

                foreach (var subscriber in groupSubscribers)
                {
                    await _botService.SendMessageAsync(subscriber.ChatId, tomorrowMessage, cancellationToken);
                    await Task.Delay(50, cancellationToken);
                }
            }

            // Notify about today's changes
            if (todayChanges.Count > 0)
            {
                var message = BuildChangeNotification(groupName, todayChanges, newData.Update, "Сьогодні");

                _logger.LogInformation("Sending today changes notification to {Count} subscribers of group {Group}",
                    groupSubscribers.Count, groupName);

                foreach (var subscriber in groupSubscribers)
                {
                    await _botService.SendMessageAsync(subscriber.ChatId, message, cancellationToken);
                    await Task.Delay(50, cancellationToken);
                }
            }

            // Notify about tomorrow's changes (if it was already available before)
            if (!tomorrowJustAppeared && tomorrowChanges.Count > 0)
            {
                var message = BuildChangeNotification(groupName, tomorrowChanges, newData.Update, "Завтра");

                _logger.LogInformation("Sending tomorrow changes notification to {Count} subscribers of group {Group}",
                    groupSubscribers.Count, groupName);

                foreach (var subscriber in groupSubscribers)
                {
                    await _botService.SendMessageAsync(subscriber.ChatId, message, cancellationToken);
                    await Task.Delay(50, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Gets the changes for a specific group between old and new data for a specific day
    /// </summary>
    private List<ScheduleChange> GetGroupChanges(
        DtekScheduleData oldData,
        DtekScheduleData newData,
        string groupName,
        long dayTimestamp)
    {
        var changes = new List<ScheduleChange>();
        var timestampKey = dayTimestamp.ToString();

        if (!newData.Data.TryGetValue(timestampKey, out var newDayData))
            return changes;

        if (!newDayData.TryGetValue(groupName, out var newGroupData))
            return changes;

        // Get old data for comparison
        Dictionary<string, string>? oldGroupData = null;
        if (oldData.Data.TryGetValue(timestampKey, out var oldDayData))
        {
            oldDayData.TryGetValue(groupName, out oldGroupData);
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
        string updateTime,
        string dayLabel)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"🔔 <b>Зміни в графіку {groupName}</b> | {dayLabel}");
        sb.AppendLine($"🕐 Оновлено: {updateTime}");
        sb.AppendLine();

        foreach (var change in changes.OrderBy(c => c.Hour))
        {
            // Key "1" = 00:00-01:00, Key "2" = 01:00-02:00, etc.
            var startHour = (change.Hour - 1).ToString("D2");
            var endHour = change.Hour == 24 ? "00" : change.Hour.ToString("D2");
            
            var oldStatusText = change.OldStatus is not null 
                ? PowerStatus.ToDisplayString(change.OldStatus) 
                : "❓ Невідомо";
            var newStatusText = PowerStatus.ToDisplayString(change.NewStatus);

            sb.AppendLine($"<code>{startHour}-{endHour}</code>");
            sb.AppendLine($"   {oldStatusText} → {newStatusText}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds notification when tomorrow's schedule just appeared
    /// </summary>
    private string BuildTomorrowAppearedNotification(string groupName, DtekScheduleData data)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"📆 <b>З'явився розклад на завтра!</b>");
        sb.AppendLine($"Група: <b>{groupName}</b>");
        sb.AppendLine($"🕐 Оновлено: {data.Update}");
        sb.AppendLine();

        var tomorrowTimestamp = ScheduleFormatter.GetTomorrowTimestamp(data.Today);
        
        if (data.Data.TryGetValue(tomorrowTimestamp.ToString(), out var tomorrowData) &&
            tomorrowData.TryGetValue(groupName, out var groupData))
        {
            var dateTime = ScheduleFormatter.TimestampToDateTime(tomorrowTimestamp);
            sb.AppendLine($"📅 {dateTime:dd.MM.yyyy}");
            sb.AppendLine();

            // Show brief summary
            var noLightHours = groupData.Count(kvp => kvp.Value == PowerStatus.No);
            var partialHours = groupData.Count(kvp => kvp.Value == PowerStatus.First || kvp.Value == PowerStatus.Second);

            if (noLightHours > 0)
            {
                sb.AppendLine($"🔴 Відключення: {noLightHours} год.");
            }
            if (partialHours > 0)
            {
                sb.AppendLine($"⚠️ Частково: {partialHours} год.");
            }
            if (noLightHours == 0 && partialHours == 0)
            {
                sb.AppendLine("✅ Відключень не заплановано!");
            }

            sb.AppendLine();
            sb.AppendLine("Використовуйте /schedule щоб переглянути детальний розклад.");
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


