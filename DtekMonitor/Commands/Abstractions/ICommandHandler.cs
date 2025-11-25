using Telegram.Bot;
using Telegram.Bot.Types;

namespace DtekMonitor.Commands.Abstractions;

/// <summary>
/// Interface for handling Telegram bot commands
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// The command name without the leading slash (e.g., "start", "setgroup")
    /// </summary>
    string CommandName { get; }

    /// <summary>
    /// Description of the command for help messages
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Determines if this handler can process the given message
    /// </summary>
    bool CanHandle(Message message);

    /// <summary>
    /// Executes the command handler pipeline
    /// </summary>
    Task RunCommandHandlerPipelineAsync(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken = default);
}


