using DtekMonitor.Commands;
using DtekMonitor.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DtekMonitor.Services;

/// <summary>
/// Background service that handles Telegram bot polling and message processing
/// </summary>
public class BotService : BackgroundService
{
    private readonly ILogger<BotService> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceScopeFactory _scopeFactory;

    public BotService(
        ILogger<BotService> logger,
        IOptions<TelegramSettings> telegramSettings,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;

        var token = telegramSettings.Value.BotToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Telegram bot token is not configured. Set TELEGRAM__BOTTOKEN environment variable.");
        }

        _botClient = new TelegramBotClient(token);
    }

    /// <summary>
    /// Gets the bot client for sending messages from other services
    /// </summary>
    public ITelegramBotClient BotClient => _botClient;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
            DropPendingUpdates = true
        };

        try
        {
            var me = await _botClient.GetMe(stoppingToken);
            _logger.LogInformation("Bot started: @{Username} (ID: {Id})", me.Username, me.Id);

            await _botClient.ReceiveAsync(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Bot service is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot service encountered an error");
            throw;
        }
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken)
    {
        // Handle callback queries (button presses)
        if (update.CallbackQuery is { } callbackQuery)
        {
            await HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            return;
        }

        if (update.Message is not { } message)
            return;

        _logger.LogDebug("Received message from {ChatId}: {Text}",
            message.Chat.Id,
            message.Text ?? "[no text]");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var commandRegistry = scope.ServiceProvider.GetRequiredService<CommandHandlerRegistry>();

            var handled = await commandRegistry.HandleMessageAsync(botClient, message, cancellationToken);

            if (!handled && message.Text?.StartsWith('/') == true)
            {
                await botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "❓ Невідома команда. Використовуйте /start для перегляду доступних команд.",
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from {ChatId}", message.Chat.Id);
        }
    }

    private async Task HandleCallbackQueryAsync(
        ITelegramBotClient botClient,
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.Data is null || callbackQuery.Message is null)
            return;

        _logger.LogDebug("Received callback from {ChatId}: {Data}",
            callbackQuery.Message.Chat.Id,
            callbackQuery.Data);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var callbackHandler = scope.ServiceProvider.GetRequiredService<CallbackQueryHandler>();

            await callbackHandler.HandleAsync(botClient, callbackQuery, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing callback from {ChatId}", callbackQuery.Message.Chat.Id);
            
            // Answer callback to remove loading state
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "❌ Помилка обробки",
                cancellationToken: cancellationToken);
        }
    }

    private Task HandlePollingErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error [{apiRequestException.ErrorCode}]: {apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError("Telegram polling error: {Error}", errorMessage);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a message to a specific chat
    /// </summary>
    public async Task SendMessageAsync(
        long chatId,
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 403)
        {
            _logger.LogWarning("Bot was blocked by user {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to {ChatId}", chatId);
        }
    }
}


