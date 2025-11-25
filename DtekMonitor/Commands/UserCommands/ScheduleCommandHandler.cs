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
            if (!DtekGroups.IsValidGroup(groupName))
            {
                sb.AppendLine($"❌ Невідома група: <code>{parameters}</code>");
                sb.AppendLine();
                sb.AppendLine("📊 <b>Доступні групи:</b>");
                sb.AppendLine($"<code>{string.Join(", ", DtekGroups.AllGroups)}</code>");
                return sb.ToString();
            }
        }
        else if (subscriber is not null)
        {
            groupName = subscriber.GroupName;
        }
        else
        {
            sb.AppendLine("❌ Ви не підписані на жодну групу.");
            sb.AppendLine();
            sb.AppendLine("Використовуйте /setgroup щоб підписатися, або вкажіть групу:");
            sb.AppendLine("<code>/schedule GPV4.1</code>");
            return sb.ToString();
        }

        // Get current schedule data
        var scheduleData = _scraper.GetLastData();

        if (scheduleData is null)
        {
            sb.AppendLine("⏳ Дані ще завантажуються. Спробуйте через хвилину.");
            return sb.ToString();
        }

        sb.AppendLine($"📊 <b>Графік відключень для групи {groupName}</b>");
        sb.AppendLine($"🕐 Оновлено: {scheduleData.Update}");
        sb.AppendLine();

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

        sb.AppendLine("<b>Сьогодні:</b>");
        sb.AppendLine();
        
        // Display schedule in a compact format
        var currentHour = DateTime.Now.Hour + 1; // Hours in data are 1-24
        
        for (int hour = 1; hour <= 24; hour++)
        {
            var hourKey = hour.ToString();
            var status = groupData.TryGetValue(hourKey, out var s) ? s : "?";
            var statusIcon = PowerStatus.ToShortDisplayString(status);
            
            var hourDisplay = hour == 24 ? "00" : hour.ToString("D2");
            var nextHour = hour == 24 ? "01" : (hour + 1).ToString("D2");
            
            var marker = hour == currentHour ? "👉 " : "   ";
            
            sb.AppendLine($"{marker}<code>{hourDisplay}:00-{nextHour}:00</code> {statusIcon}");
        }

        sb.AppendLine();
        sb.AppendLine("<b>Легенда:</b>");
        sb.AppendLine("✅ - світло є");
        sb.AppendLine("🔴 - світла немає");
        sb.AppendLine("⚠️½ - частково (перша половина)");
        sb.AppendLine("½⚠️ - частково (друга половина)");

        return sb.ToString();
    }
}


