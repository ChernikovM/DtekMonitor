using DtekMonitor.Database;
using DtekMonitor.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DtekMonitor.Services;

/// <summary>
/// Handles callback queries from inline keyboard buttons
/// </summary>
public class CallbackQueryHandler
{
    private readonly ILogger<CallbackQueryHandler> _logger;
    private readonly AppDbContext _dbContext;
    private readonly DtekScraper _scraper;

    // Callback data prefixes
    private const string SetGroupPrefix = "setgroup:";
    private const string SchedulePrefix = "schedule:";

    public CallbackQueryHandler(
        ILogger<CallbackQueryHandler> logger,
        AppDbContext dbContext,
        DtekScraper scraper)
    {
        _logger = logger;
        _dbContext = dbContext;
        _scraper = scraper;
    }

    public async Task HandleAsync(
        ITelegramBotClient botClient,
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        var data = callbackQuery.Data!;
        var chatId = callbackQuery.Message!.Chat.Id;
        var messageId = callbackQuery.Message.MessageId;

        if (data.StartsWith(SetGroupPrefix))
        {
            await HandleSetGroupAsync(botClient, callbackQuery, data[SetGroupPrefix.Length..], cancellationToken);
        }
        else if (data.StartsWith(SchedulePrefix))
        {
            await HandleScheduleAsync(botClient, callbackQuery, data[SchedulePrefix.Length..], cancellationToken);
        }
        else
        {
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "❓ Невідома дія",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleSetGroupAsync(
        ITelegramBotClient botClient,
        CallbackQuery callbackQuery,
        string groupInput,
        CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var messageId = callbackQuery.Message.MessageId;

        if (!DtekGroups.IsValidGroup(groupInput))
        {
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "❌ Невідома група",
                cancellationToken: cancellationToken);
            return;
        }

        // Normalize to API format for storage
        var apiGroupName = DtekGroups.Normalize(groupInput);
        var displayGroupName = DtekGroups.ToDisplayName(apiGroupName);

        // Update or create subscription
        var subscriber = await _dbContext.Subscribers
            .FirstOrDefaultAsync(s => s.ChatId == chatId, cancellationToken);

        if (subscriber is null)
        {
            subscriber = new Subscriber
            {
                ChatId = chatId,
                GroupName = apiGroupName,
                Username = callbackQuery.From.Username,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Subscribers.Add(subscriber);
            _logger.LogInformation("New subscriber via button: ChatId={ChatId}, Group={Group}", chatId, apiGroupName);
        }
        else
        {
            subscriber.GroupName = apiGroupName;
            subscriber.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Updated subscriber via button: ChatId={ChatId}, Group={Group}", chatId, apiGroupName);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Update the message to show confirmation (display name for user)
        var responseText = $"✅ Ви підписані на чергу <b>{displayGroupName}</b>!\n\n" +
                          "Тепер ви будете отримувати сповіщення про зміни в графіку.\n\n" +
                          "Натисніть кнопку нижче щоб переглянути розклад:";

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📅 Сьогодні", $"{SchedulePrefix}{apiGroupName}:today"),
                InlineKeyboardButton.WithCallbackData("📆 Завтра", $"{SchedulePrefix}{apiGroupName}:tomorrow")
            }
        });

        await botClient.EditMessageText(
            chatId: chatId,
            messageId: messageId,
            text: responseText,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);

        await botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            $"✅ Черга {displayGroupName}",
            cancellationToken: cancellationToken);
    }

    private async Task HandleScheduleAsync(
        ITelegramBotClient botClient,
        CallbackQuery callbackQuery,
        string parameters,
        CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var messageId = callbackQuery.Message.MessageId;

        // Parse parameters: "GPV4.1:today" or "GPV4.1:tomorrow"
        var parts = parameters.Split(':');
        if (parts.Length != 2)
        {
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "❌ Невірний формат",
                cancellationToken: cancellationToken);
            return;
        }

        var groupName = parts[0];
        var dayType = parts[1]; // "today" or "tomorrow"

        var scheduleData = _scraper.GetLastData();
        if (scheduleData is null)
        {
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "⏳ Дані завантажуються...",
                cancellationToken: cancellationToken);
            return;
        }

        // Determine which day to show
        long timestamp;
        string dayLabel;
        bool showCurrentHour;

        if (dayType == "tomorrow")
        {
            timestamp = ScheduleFormatter.GetTomorrowTimestamp(scheduleData.Today);
            dayLabel = "Завтра";
            showCurrentHour = false;
        }
        else
        {
            timestamp = scheduleData.Today;
            dayLabel = "Сьогодні";
            showCurrentHour = true;
        }

        var timestampKey = timestamp.ToString();

        if (!scheduleData.Data.TryGetValue(timestampKey, out var dayData))
        {
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                dayType == "tomorrow" ? "📆 Розклад на завтра ще недоступний" : "❌ Дані недоступні",
                showAlert: true,
                cancellationToken: cancellationToken);
            return;
        }

        if (!dayData.TryGetValue(groupName, out var groupData))
        {
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                $"❌ Дані для {groupName} недоступні",
                cancellationToken: cancellationToken);
            return;
        }

        var dateTime = ScheduleFormatter.TimestampToDateTime(timestamp);
        var scheduleText = ScheduleFormatter.FormatDaySchedule(groupData, groupName, dayLabel, dateTime, showCurrentHour);
        scheduleText += $"\n🕐 Оновлено: {scheduleData.Update}";

        // Create keyboard with day selection
        var keyboard = CreateScheduleKeyboard(groupName, dayType, ScheduleFormatter.IsTomorrowAvailable(scheduleData));

        await botClient.EditMessageText(
            chatId: chatId,
            messageId: messageId,
            text: scheduleText,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);

        await botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates inline keyboard for schedule navigation
    /// </summary>
    public static InlineKeyboardMarkup CreateScheduleKeyboard(string groupName, string currentDay, bool tomorrowAvailable)
    {
        var todayButton = currentDay == "today"
            ? InlineKeyboardButton.WithCallbackData("📅 Сьогодні ✓", $"{SchedulePrefix}{groupName}:today")
            : InlineKeyboardButton.WithCallbackData("📅 Сьогодні", $"{SchedulePrefix}{groupName}:today");

        var tomorrowText = tomorrowAvailable ? "📆 Завтра" : "📆 Завтра (немає)";
        var tomorrowButton = currentDay == "tomorrow"
            ? InlineKeyboardButton.WithCallbackData($"{tomorrowText} ✓", $"{SchedulePrefix}{groupName}:tomorrow")
            : InlineKeyboardButton.WithCallbackData(tomorrowText, $"{SchedulePrefix}{groupName}:tomorrow");

        return new InlineKeyboardMarkup(new[]
        {
            new[] { todayButton, tomorrowButton }
        });
    }

    /// <summary>
    /// Creates inline keyboard for group selection (using display names like "1.1", "3.2")
    /// </summary>
    public static InlineKeyboardMarkup CreateGroupSelectionKeyboard()
    {
        var buttons = new List<InlineKeyboardButton[]>();

        // Create 2 buttons per row using display names
        for (int i = 0; i < DtekGroups.DisplayGroups.Length; i += 2)
        {
            var displayName1 = DtekGroups.DisplayGroups[i];
            var apiName1 = DtekGroups.ApiGroups[i];
            
            var row = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"Черга {displayName1}", $"{SetGroupPrefix}{apiName1}")
            };

            if (i + 1 < DtekGroups.DisplayGroups.Length)
            {
                var displayName2 = DtekGroups.DisplayGroups[i + 1];
                var apiName2 = DtekGroups.ApiGroups[i + 1];
                row.Add(InlineKeyboardButton.WithCallbackData($"Черга {displayName2}", $"{SetGroupPrefix}{apiName2}"));
            }

            buttons.Add(row.ToArray());
        }

        return new InlineKeyboardMarkup(buttons);
    }
}

