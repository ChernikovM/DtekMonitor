using System.Text;
using DtekMonitor.Database;
using DtekMonitor.Models;
using DtekMonitor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Spacebar.Bedrock.Telegram.Core.Commands;
using Spacebar.Bedrock.Telegram.Core.Pipeline;

namespace DtekMonitor.Commands.UserCommands;

/// <summary>
/// Handles the /stop command - unsubscribes user from notifications
/// </summary>
public class StopCommandHandler : CommandHandler<StopCommandHandler>
{
    public StopCommandHandler(ILogger<StopCommandHandler> logger) : base(logger)
    {
    }

    public override string CommandName => "stop";
    public override string Description => "Відписатися від сповіщень";

    protected override async Task<string?> ExecuteAsync(UpdateContext context)
    {
        var sb = new StringBuilder();

        var dbContext = context.GetRequiredService<AppDbContext>();

        var subscriber = await dbContext.Subscribers
            .FirstOrDefaultAsync(s => s.ChatId == context.ChatId, context.CancellationToken);

        if (subscriber is null)
        {
            sb.AppendLine("ℹ️ Ви не були підписані на сповіщення.");
            sb.AppendLine();
            sb.AppendLine("Натисніть <b>📊 Обрати групу</b> щоб підписатися.");
        }
        else
        {
            var apiGroupName = subscriber.GroupName;
            var displayGroupName = DtekGroups.ToDisplayName(apiGroupName);
            dbContext.Subscribers.Remove(subscriber);
            await dbContext.SaveChangesAsync(context.CancellationToken);

            Logger.LogInformation("Subscriber removed: ChatId={ChatId}, Group={Group}", context.ChatId, apiGroupName);

            sb.AppendLine($"✅ Ви успішно відписалися від сповіщень черги <b>{displayGroupName}</b>.");
            sb.AppendLine();
            sb.AppendLine("Натисніть <b>📊 Обрати групу</b> щоб підписатися знову.");
        }

        await SendTextMessageWithKeyboardAsync(context, sb.ToString(), KeyboardMarkups.MainMenuKeyboard);

        return null;
    }
}
