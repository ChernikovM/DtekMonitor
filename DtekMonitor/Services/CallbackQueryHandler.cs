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
        string groupName,
        CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var messageId = callbackQuery.Message.MessageId;

        if (!DtekGroups.IsValidGroup(groupName))
        {
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "❌ Невідома група",
                cancellationToken: cancellationToken);
            return;
        }

        // Update or create subscription
        var subscriber = await _dbContext.Subscribers
            .FirstOrDefaultAsync(s => s.ChatId == chatId, cancellationToken);

        if (subscriber is null)
        {
            subscriber = new Subscriber
            {
                ChatId = chatId,
                GroupName = groupName,
                Username = callbackQuery.From.Username,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Subscribers.Add(subscriber);
            _logger.LogInformation("New subscriber via button: ChatId={ChatId}, Group={Group}", chatId, groupName);
        }
        else
        {
            subscriber.GroupName = groupName;
            subscriber.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Updated subscriber via button: ChatId={ChatId}, Group={Group}", chatId, groupName);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Update the message to show confirmation
        var responseText = $"✅ Ви підписані на групу <b>{groupName}</b>!\n\n" +
                          "Тепер ви будете отримувати сповіщення про зміни в графіку.\n\n" +
                          "Натисніть кнопку нижче щоб переглянути розклад:";

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📅 Сьогодні", $"{SchedulePrefix}{groupName}:today"),
                InlineKeyboardButton.WithCallbackData("📆 Завтра", $"{SchedulePrefix}{groupName}:tomorrow")
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
            $"✅ Підписано на {groupName}",
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
    /// Creates inline keyboard for group selection
    /// </summary>
    public static InlineKeyboardMarkup CreateGroupSelectionKeyboard()
    {
        var buttons = new List<InlineKeyboardButton[]>();

        // Create 2 buttons per row
        for (int i = 0; i < DtekGroups.AllGroups.Length; i += 2)
        {
            var row = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(DtekGroups.AllGroups[i], $"{SetGroupPrefix}{DtekGroups.AllGroups[i]}")
            };

            if (i + 1 < DtekGroups.AllGroups.Length)
            {
                row.Add(InlineKeyboardButton.WithCallbackData(DtekGroups.AllGroups[i + 1], $"{SetGroupPrefix}{DtekGroups.AllGroups[i + 1]}"));
            }

            buttons.Add(row.ToArray());
        }

        return new InlineKeyboardMarkup(buttons);
    }
}

