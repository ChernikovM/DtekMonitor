using System.Text;
using DtekMonitor.Commands.Abstractions;
using DtekMonitor.Database;
using DtekMonitor.Models;
using DtekMonitor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DtekMonitor.Commands.UserCommands;

/// <summary>
/// Handles the /setgroup command - subscribes user to a specific DTEK group
/// </summary>
public class SetGroupCommandHandler : CommandHandler<SetGroupCommandHandler>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SetGroupCommandHandler(
        ILogger<SetGroupCommandHandler> logger,
        IServiceScopeFactory scopeFactory) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    public override string CommandName => "setgroup";
    public override string Description => "Підписатися на групу відключень";

    protected override async Task<string?> HandleCommandAsync(
        ITelegramBotClient botClient,
        Message message,
        string? parameters,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        // If no parameters - show inline keyboard with all groups
        if (string.IsNullOrWhiteSpace(parameters))
        {
            sb.AppendLine("📊 <b>Оберіть вашу групу відключень:</b>");
            sb.AppendLine();
            sb.AppendLine("Натисніть на кнопку з номером вашої групи:");

            var keyboard = CallbackQueryHandler.CreateGroupSelectionKeyboard();

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: sb.ToString(),
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);

            return null; // Don't send another message
        }

        var groupName = DtekGroups.Normalize(parameters);

        if (!DtekGroups.IsValidGroup(groupName))
        {
            sb.AppendLine($"❌ Невідома група: <code>{parameters}</code>");
            sb.AppendLine();
            sb.AppendLine("📊 <b>Доступні групи:</b>");
            sb.AppendLine($"<code>{string.Join(", ", DtekGroups.AllGroups)}</code>");
            return sb.ToString();
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var subscriber = await dbContext.Subscribers
            .FirstOrDefaultAsync(s => s.ChatId == message.Chat.Id, cancellationToken);

        if (subscriber is null)
        {
            subscriber = new Subscriber
            {
                ChatId = message.Chat.Id,
                GroupName = groupName,
                Username = message.Chat.Username,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Subscribers.Add(subscriber);
            Logger.LogInformation("New subscriber: ChatId={ChatId}, Group={Group}", message.Chat.Id, groupName);
        }
        else
        {
            var oldGroup = subscriber.GroupName;
            subscriber.GroupName = groupName;
            subscriber.Username = message.Chat.Username;
            subscriber.UpdatedAt = DateTime.UtcNow;
            Logger.LogInformation("Updated subscriber: ChatId={ChatId}, OldGroup={OldGroup}, NewGroup={NewGroup}",
                message.Chat.Id, oldGroup, groupName);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        sb.AppendLine($"✅ Ви успішно підписані на групу <b>{groupName}</b>!");
        sb.AppendLine();
        sb.AppendLine("Тепер ви будете отримувати сповіщення про зміни в графіку відключень.");
        sb.AppendLine();
        sb.AppendLine("Натисніть <b>📅 Розклад</b> щоб переглянути графік.");

        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardMarkups.MainMenuKeyboard,
            cancellationToken: cancellationToken);

        return null;
    }
}


