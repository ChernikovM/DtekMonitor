using System.Text;
using DtekMonitor.Commands.Abstractions;
using DtekMonitor.Models;
using DtekMonitor.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DtekMonitor.Commands.UserCommands;

/// <summary>
/// Handles the /start command - shows welcome message and available groups
/// </summary>
public class StartCommandHandler : CommandHandler<StartCommandHandler>
{
    public StartCommandHandler(ILogger<StartCommandHandler> logger) : base(logger)
    {
    }

    public override string CommandName => "start";
    public override string Description => "Почати роботу з ботом";

    protected override async Task<string?> HandleCommandAsync(
        ITelegramBotClient botClient,
        Message message,
        string? parameters,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        sb.AppendLine("👋 <b>Вітаю!</b>");
        sb.AppendLine();
        sb.AppendLine("Цей бот допоможе вам відстежувати графіки відключення світла ДТЕК.");
        sb.AppendLine();
        sb.AppendLine("Використовуйте кнопки меню внизу 👇 або команди:");
        sb.AppendLine();
        sb.AppendLine("📋 <b>Доступні команди:</b>");
        sb.AppendLine("• /setgroup - Обрати групу відключень");
        sb.AppendLine("• /schedule - Графік на сьогодні/завтра");
        sb.AppendLine("• /mygroup - Моя поточна група");
        sb.AppendLine("• /stop - Відписатися");
        sb.AppendLine();
        sb.AppendLine("💡 Натисніть <b>📊 Обрати групу</b> щоб почати!");

        // Send message with persistent keyboard
        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            replyMarkup: KeyboardMarkups.MainMenuKeyboard,
            cancellationToken: cancellationToken);

        return null; // Don't send another message
    }
}


