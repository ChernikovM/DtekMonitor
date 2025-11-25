using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DtekMonitor.Commands.Abstractions;

/// <summary>
/// Base class for command handlers with common functionality
/// </summary>
/// <typeparam name="TCommand">The type of the command handler for logging</typeparam>
public abstract class CommandHandler<TCommand> : ICommandHandler
{
    protected ILogger<TCommand> Logger { get; }

    protected CommandHandler(ILogger<TCommand> logger)
    {
        Logger = logger;
    }

    /// <inheritdoc />
    public abstract string CommandName { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <summary>
    /// Handles the command logic. Override this in derived classes.
    /// </summary>
    /// <param name="botClient">The Telegram bot client</param>
    /// <param name="message">The incoming message</param>
    /// <param name="parameters">Command parameters (text after the command)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response text to send back, or null if no response needed</returns>
    protected abstract Task<string?> HandleCommandAsync(
        ITelegramBotClient botClient,
        Message message,
        string? parameters,
        CancellationToken cancellationToken);

    /// <summary>
    /// Extracts parameters from the message text
    /// </summary>
    protected virtual string? GetCommandParameters(string? messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return null;

        var parts = messageText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1].Trim() : null;
    }

    /// <inheritdoc />
    public virtual bool CanHandle(Message message)
    {
        return CanHandleText(message.Text);
    }

    /// <inheritdoc />
    public virtual bool CanHandleText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (!text.StartsWith('/'))
            return false;

        var commandPart = text.Split(' ')[0].TrimStart('/');
        
        // Handle commands with bot username (e.g., /start@MyBot)
        var atIndex = commandPart.IndexOf('@');
        if (atIndex > 0)
            commandPart = commandPart[..atIndex];

        return commandPart.Equals(CommandName, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public virtual async Task RunCommandHandlerPipelineAsync(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken = default)
    {
        await RunCommandHandlerPipelineAsync(botClient, message, message.Text, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task RunCommandHandlerPipelineAsync(
        ITelegramBotClient botClient,
        Message message,
        string? commandText,
        CancellationToken cancellationToken = default)
    {
        string? responseText;
        var parameters = GetCommandParameters(commandText);

        try
        {
            Logger.LogDebug("Handling command /{Command} from chat {ChatId}", CommandName, message.Chat.Id);
            responseText = await HandleCommandAsync(botClient, message, parameters, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling command /{Command}: {Message}", CommandName, ex.Message);
            responseText = "❌ Виникла помилка при обробці команди. Спробуйте пізніше.";
        }

        if (!string.IsNullOrEmpty(responseText))
        {
            await SendMessageAsync(botClient, message.Chat.Id, responseText, cancellationToken);
        }
    }

    /// <summary>
    /// Sends a message to the specified chat
    /// </summary>
    protected async Task SendMessageAsync(
        ITelegramBotClient botClient,
        long chatId,
        string text,
        CancellationToken cancellationToken = default,
        int? replyToMessageId = null)
    {
        await botClient.SendMessage(
            chatId: chatId,
            text: text,
            parseMode: ParseMode.Html,
            replyParameters: replyToMessageId.HasValue
                ? new ReplyParameters { MessageId = replyToMessageId.Value }
                : null,
            cancellationToken: cancellationToken);
    }
}


