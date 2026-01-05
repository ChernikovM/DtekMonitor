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
/// Handles the /mygroup command - shows user's current subscription
/// </summary>
public class MyGroupCommandHandler : CommandHandler<MyGroupCommandHandler>
{
    public MyGroupCommandHandler(ILogger<MyGroupCommandHandler> logger) : base(logger)
    {
    }

    public override string CommandName => "mygroup";
    public override string Description => "Показати мою групу підписки";
    public override IReadOnlyList<string> Aliases => ["ℹ️ Моя група"];

    protected override async Task<string?> ExecuteAsync(UpdateContext context)
    {
        var sb = new StringBuilder();

        var dbContext = context.GetRequiredService<AppDbContext>();

        var subscriber = await dbContext.Subscribers
            .FirstOrDefaultAsync(s => s.ChatId == context.ChatId, context.CancellationToken);

        if (subscriber is null)
        {
            sb.AppendLine("❌ Ви ще не підписані на жодну чергу.");
            sb.AppendLine();
            sb.AppendLine("Натисніть <b>📊 Обрати групу</b> щоб підписатися.");
        }
        else
        {
            var displayGroupName = DtekGroups.ToDisplayName(subscriber.GroupName);
            sb.AppendLine($"✅ Ваша поточна черга: <b>{displayGroupName}</b>");
            sb.AppendLine();
            sb.AppendLine($"📅 Підписка створена: {subscriber.CreatedAt:dd.MM.yyyy HH:mm}");
            
            if (subscriber.UpdatedAt != subscriber.CreatedAt)
            {
                sb.AppendLine($"🔄 Останнє оновлення: {subscriber.UpdatedAt:dd.MM.yyyy HH:mm}");
            }
        }

        await SendTextMessageWithKeyboardAsync(context, sb.ToString(), KeyboardMarkups.MainMenuKeyboard);

        return null;
    }
}
