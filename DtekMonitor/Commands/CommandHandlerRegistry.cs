using System.Reflection;
using DtekMonitor.Commands.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DtekMonitor.Commands;

/// <summary>
/// Registry for command handlers with automatic discovery and registration
/// </summary>
public class CommandHandlerRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CommandHandlerRegistry> _logger;

    public CommandHandlerRegistry(
        IServiceProvider serviceProvider,
        ILogger<CommandHandlerRegistry> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Registers all command handlers from the specified assembly
    /// </summary>
    public static void RegisterAllHandlers(
        IServiceCollection services,
        Assembly? assembly = null)
    {
        assembly ??= Assembly.GetExecutingAssembly();

        services.AddScoped<CommandHandlerRegistry>();

        var handlerTypes = assembly.GetTypes()
            .Where(t =>
                t is { IsClass: true, IsAbstract: false } &&
                typeof(ICommandHandler).IsAssignableFrom(t));

        foreach (var handlerType in handlerTypes)
        {
            services.AddTransient(typeof(ICommandHandler), handlerType);
            services.AddTransient(handlerType);
        }
    }

    /// <summary>
    /// Finds a handler that can process the given message
    /// </summary>
    public ICommandHandler? FindHandler(Message message)
    {
        var handlers = _serviceProvider.GetServices<ICommandHandler>();
        return handlers.FirstOrDefault(h => h.CanHandle(message));
    }

    /// <summary>
    /// Gets all registered command descriptions for help messages
    /// </summary>
    public Dictionary<string, string> GetCommandDescriptions()
    {
        var handlers = _serviceProvider.GetServices<ICommandHandler>();
        return handlers.ToDictionary(
            h => h.CommandName,
            h => h.Description);
    }

    /// <summary>
    /// Handles an incoming message by finding and executing the appropriate handler
    /// </summary>
    public async Task<bool> HandleMessageAsync(
        ITelegramBotClient botClient,
        Message message,
        CancellationToken cancellationToken = default)
    {
        return await HandleMessageAsync(botClient, message, message.Text, cancellationToken);
    }

    /// <summary>
    /// Handles an incoming message with overridden command text (for keyboard button mapping)
    /// </summary>
    public async Task<bool> HandleMessageAsync(
        ITelegramBotClient botClient,
        Message message,
        string? commandText,
        CancellationToken cancellationToken = default)
    {
        var handler = FindHandlerByText(commandText);

        if (handler is null)
        {
            _logger.LogDebug("No handler found for command: {Text}", commandText);
            return false;
        }

        await handler.RunCommandHandlerPipelineAsync(botClient, message, commandText, cancellationToken);
        return true;
    }

    /// <summary>
    /// Finds a handler by command text
    /// </summary>
    private ICommandHandler? FindHandlerByText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var handlers = _serviceProvider.GetServices<ICommandHandler>();
        return handlers.FirstOrDefault(h => h.CanHandleText(text));
    }
}


