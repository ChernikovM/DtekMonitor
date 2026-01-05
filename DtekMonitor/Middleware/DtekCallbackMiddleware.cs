using DtekMonitor.Database;
using DtekMonitor.Models;
using DtekMonitor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Spacebar.Bedrock.Telegram.Core.Abstractions;
using Spacebar.Bedrock.Telegram.Core.Pipeline;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DtekMonitor.Middleware;

/// <summary>
/// Middleware that handles callback queries from inline keyboard buttons.
/// Handles: setgroup:{group}, schedule:{group}:{day}
/// </summary>
public class DtekCallbackMiddleware : ITelegramMiddleware
{
    private readonly ILogger<DtekCallbackMiddleware> _logger;

    // Callback data prefixes
    private const string SetGroupPrefix = "setgroup:";
    private const string SchedulePrefix = "schedule:";

    public DtekCallbackMiddleware(ILogger<DtekCallbackMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(UpdateContext context, TelegramMiddlewareDelegate next)
    {
        // Only handle callback queries
        if (context.CallbackQuery is null || string.IsNullOrEmpty(context.CallbackQuery.Data))
        {
            await next(context);
            return;
        }

        var data = context.CallbackQuery.Data;
        var handled = false;

        try
        {
            if (data.StartsWith(SetGroupPrefix))
            {
                await HandleSetGroupAsync(context, data[SetGroupPrefix.Length..]);
                handled = true;
            }
            else if (data.StartsWith(SchedulePrefix))
            {
                await HandleScheduleAsync(context, data[SchedulePrefix.Length..]);
                handled = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling callback: {Data}", data);
            
            await context.BotClient.AnswerCallbackQuery(
                context.CallbackQuery.Id,
                "❌ Помилка обробки",
                cancellationToken: context.CancellationToken);
        }

        if (!handled)
        {
            // Pass to next middleware if not handled
            await next(context);
        }
    }

    private async Task HandleSetGroupAsync(UpdateContext context, string groupInput)
    {
        var callbackQuery = context.CallbackQuery!;
        var chatId = callbackQuery.Message!.Chat.Id;
        var messageId = callbackQuery.Message.MessageId;

        if (!DtekGroups.IsValidGroup(groupInput))
        {
            await context.BotClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "❌ Невідома група",
                cancellationToken: context.CancellationToken);
            return;
        }

        // Normalize to API format for storage
        var apiGroupName = DtekGroups.Normalize(groupInput);
        var displayGroupName = DtekGroups.ToDisplayName(apiGroupName);

        // Get DbContext from scoped services
        var dbContext = context.GetRequiredService<AppDbContext>();

        // Update or create subscription
        var subscriber = await dbContext.Subscribers
            .FirstOrDefaultAsync(s => s.ChatId == chatId, context.CancellationToken);

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
            dbContext.Subscribers.Add(subscriber);
            _logger.LogInformation("New subscriber via button: ChatId={ChatId}, Group={Group}", chatId, apiGroupName);
        }
        else
        {
            subscriber.GroupName = apiGroupName;
            subscriber.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Updated subscriber via button: ChatId={ChatId}, Group={Group}", chatId, apiGroupName);
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);

        // Update the message to show confirmation
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

        await context.BotClient.EditMessageText(
            chatId: chatId,
            messageId: messageId,
            text: responseText,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: context.CancellationToken);

        await context.BotClient.AnswerCallbackQuery(
            callbackQuery.Id,
            $"✅ Черга {displayGroupName}",
            cancellationToken: context.CancellationToken);
    }

    private async Task HandleScheduleAsync(UpdateContext context, string parameters)
    {
        var callbackQuery = context.CallbackQuery!;
        var chatId = callbackQuery.Message!.Chat.Id;
        var messageId = callbackQuery.Message.MessageId;

        // Parse parameters: "GPV4.1:today" or "GPV4.1:tomorrow"
        var parts = parameters.Split(':');
        if (parts.Length != 2)
        {
            await context.BotClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "❌ Невірний формат",
                cancellationToken: context.CancellationToken);
            return;
        }

        var groupName = parts[0];
        var dayType = parts[1]; // "today" or "tomorrow"

        var scraper = context.GetRequiredService<DtekScraper>();
        var scheduleData = scraper.GetLastData();

        if (scheduleData is null)
        {
            await context.BotClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "⏳ Дані завантажуються...",
                cancellationToken: context.CancellationToken);
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
            await context.BotClient.AnswerCallbackQuery(
                callbackQuery.Id,
                dayType == "tomorrow" ? "📆 Розклад на завтра ще недоступний" : "❌ Дані недоступні",
                showAlert: true,
                cancellationToken: context.CancellationToken);
            return;
        }

        if (!dayData.TryGetValue(groupName, out var groupData))
        {
            await context.BotClient.AnswerCallbackQuery(
                callbackQuery.Id,
                $"❌ Дані для {groupName} недоступні",
                cancellationToken: context.CancellationToken);
            return;
        }

        var dateTime = ScheduleFormatter.TimestampToDateTime(timestamp);
        var scheduleText = ScheduleFormatter.FormatDaySchedule(groupData, groupName, dayLabel, dateTime, showCurrentHour);
        scheduleText += $"\n🕐 Оновлено: {scheduleData.Update}";

        // Create keyboard with day selection
        var keyboard = ScheduleKeyboards.CreateScheduleKeyboard(groupName, dayType, ScheduleFormatter.IsTomorrowAvailable(scheduleData));

        await context.BotClient.EditMessageText(
            chatId: chatId,
            messageId: messageId,
            text: scheduleText,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: context.CancellationToken);

        await context.BotClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: context.CancellationToken);
    }
}

