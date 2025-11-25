using System.Text;
using DtekMonitor.Commands.Abstractions;
using DtekMonitor.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DtekMonitor.Commands.UserCommands;

/// <summary>
/// Handles the /stop command - unsubscribes user from notifications
/// </summary>
public class StopCommandHandler : CommandHandler<StopCommandHandler>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public StopCommandHandler(
        ILogger<StopCommandHandler> logger,
        IServiceScopeFactory scopeFactory) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    public override string CommandName => "stop";
    public override string Description => "Відписатися від сповіщень";

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

        if (subscriber is null)
        {
            sb.AppendLine("ℹ️ Ви не були підписані на сповіщення.");
            sb.AppendLine();
            sb.AppendLine("Використовуйте /setgroup щоб підписатися на групу.");
            return sb.ToString();
        }

        var groupName = subscriber.GroupName;
        dbContext.Subscribers.Remove(subscriber);
        await dbContext.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Subscriber removed: ChatId={ChatId}, Group={Group}", message.Chat.Id, groupName);

        sb.AppendLine($"✅ Ви успішно відписалися від сповіщень групи <b>{groupName}</b>.");
        sb.AppendLine();
        sb.AppendLine("Щоб підписатися знову, використовуйте /setgroup.");

        return sb.ToString();
    }
}


