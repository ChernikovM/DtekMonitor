using System.Text;
using DtekMonitor.Database;
using DtekMonitor.Models;
using DtekMonitor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spacebar.Bedrock.Telegram.Core.Commands;
using Spacebar.Bedrock.Telegram.Core.Pipeline;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace DtekMonitor.Commands.UserCommands;

/// <summary>
/// Handles the /schedule command - shows current schedule for user's group
/// </summary>
public class ScheduleCommandHandler : CommandHandler<ScheduleCommandHandler>
{
    public ScheduleCommandHandler(ILogger<ScheduleCommandHandler> logger) : base(logger)
    {
    }

    public override string CommandName => "schedule";
    public override string Description => "Показати поточний графік відключень";
    public override IReadOnlyList<string> Aliases => ["📅 Розклад"];

    protected override async Task<string?> ExecuteAsync(UpdateContext context)
    {
        var sb = new StringBuilder();

        // Get services from context
        var dbContext = context.GetRequiredService<AppDbContext>();
        var scraper = context.GetRequiredService<DtekScraper>();

        var subscriber = await dbContext.Subscribers
            .FirstOrDefaultAsync(s => s.ChatId == context.ChatId, context.CancellationToken);

        string groupName;
        var parameters = context.CommandParameters;
        
        // Allow checking any group with parameter, or use subscribed group
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            groupName = DtekGroups.Normalize(parameters);
            if (!DtekGroups.IsValidGroup(parameters))
            {
                sb.AppendLine($"❌ Невідома черга: <code>{parameters}</code>");
                sb.AppendLine();
                sb.AppendLine("📊 <b>Доступні черги:</b>");
                sb.AppendLine($"<code>{string.Join(", ", DtekGroups.DisplayGroups)}</code>");
                return sb.ToString();
            }
        }
        else if (subscriber is not null)
        {
            groupName = subscriber.GroupName;
        }
        else
        {
            sb.AppendLine("❌ Ви не підписані на жодну чергу.");
            sb.AppendLine();
            sb.AppendLine("Натисніть <b>📊 Обрати групу</b> щоб підписатися.");
            return sb.ToString();
        }

        // Get current schedule data
        var scheduleData = scraper.GetLastData();

        if (scheduleData is null)
        {
            sb.AppendLine("⏳ Дані ще завантажуються. Спробуйте через хвилину.");
            return sb.ToString();
        }

        // Get today's timestamp
        var todayTimestamp = scheduleData.Today.ToString();

        if (!scheduleData.Data.TryGetValue(todayTimestamp, out var todayData))
        {
            sb.AppendLine("❌ Дані на сьогодні недоступні.");
            return sb.ToString();
        }

        if (!todayData.TryGetValue(groupName, out var groupData))
        {
            sb.AppendLine($"❌ Дані для групи {groupName} недоступні.");
            return sb.ToString();
        }

        // Format schedule using the helper
        var dateTime = ScheduleFormatter.TimestampToDateTime(scheduleData.Today);
        var scheduleText = ScheduleFormatter.FormatDaySchedule(groupData, groupName, "Сьогодні", dateTime, showCurrentHourMarker: true);
        scheduleText += $"\n🕐 Оновлено: {scheduleData.Update}";

        // Create keyboard with Today/Tomorrow buttons
        var tomorrowAvailable = ScheduleFormatter.IsTomorrowAvailable(scheduleData);
        var keyboard = ScheduleKeyboards.CreateScheduleKeyboard(groupName, "today", tomorrowAvailable);

        await context.BotClient.SendMessage(
            chatId: context.ChatId!.Value,
            text: scheduleText,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: context.CancellationToken);

        return null; // Don't send another message
    }
}
