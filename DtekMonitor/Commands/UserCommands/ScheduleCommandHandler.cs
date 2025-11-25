using System.Text;
using DtekMonitor.Commands.Abstractions;
using DtekMonitor.Database;
using DtekMonitor.Models;
using DtekMonitor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DtekMonitor.Commands.UserCommands;

/// <summary>
/// Handles the /schedule command - shows current schedule for user's group
/// </summary>
public class ScheduleCommandHandler : CommandHandler<ScheduleCommandHandler>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DtekScraper _scraper;

    public ScheduleCommandHandler(
        ILogger<ScheduleCommandHandler> logger,
        IServiceScopeFactory scopeFactory,
        DtekScraper scraper) : base(logger)
    {
        _scopeFactory = scopeFactory;
        _scraper = scraper;
    }

    public override string CommandName => "schedule";
    public override string Description => "Показати поточний графік відключень";

    protected override async Task<string?> HandleCommandAsync(
        ITelegramBotClient botClient,
        Message message,
        string? parameters,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var subscriber = await dbContext.Subscribers
            .FirstOrDefaultAsync(s => s.ChatId == message.Chat.Id, cancellationToken);

        string groupName;
        
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
        var scheduleData = _scraper.GetLastData();

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
        var keyboard = CallbackQueryHandler.CreateScheduleKeyboard(groupName, "today", tomorrowAvailable);

        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: scheduleText,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);

        return null; // Don't send another message
    }
}


