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
/// Handles the /mygroup command - shows user's current subscription
/// </summary>
public class MyGroupCommandHandler : CommandHandler<MyGroupCommandHandler>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MyGroupCommandHandler(
        ILogger<MyGroupCommandHandler> logger,
        IServiceScopeFactory scopeFactory) : base(logger)
    {
        _scopeFactory = scopeFactory;
    }

    public override string CommandName => "mygroup";
    public override string Description => "Показати мою групу підписки";

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
            sb.AppendLine("❌ Ви ще не підписані на жодну групу.");
            sb.AppendLine();
            sb.AppendLine("Натисніть <b>📊 Обрати групу</b> щоб підписатися.");
        }
        else
        {
            sb.AppendLine($"✅ Ваша поточна група: <b>{subscriber.GroupName}</b>");
            sb.AppendLine();
            sb.AppendLine($"📅 Підписка створена: {subscriber.CreatedAt:dd.MM.yyyy HH:mm}");
            
            if (subscriber.UpdatedAt != subscriber.CreatedAt)
            {
                sb.AppendLine($"🔄 Останнє оновлення: {subscriber.UpdatedAt:dd.MM.yyyy HH:mm}");
            }
        }

        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardMarkups.MainMenuKeyboard,
            cancellationToken: cancellationToken);

        return null;
    }
}

