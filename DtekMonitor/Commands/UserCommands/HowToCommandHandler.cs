using System.Text;
using DtekMonitor.Commands.Abstractions;
using DtekMonitor.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DtekMonitor.Commands.UserCommands;

/// <summary>
/// Handles the /howto command - explains how to find your group on DTEK website
/// </summary>
public class HowToCommandHandler : CommandHandler<HowToCommandHandler>
{
    private const string DtekWebsiteUrl = "https://www.dtek-krem.com.ua/ua/shutdowns";

    public HowToCommandHandler(ILogger<HowToCommandHandler> logger) : base(logger)
    {
    }

    public override string CommandName => "howto";
    public override string Description => "Як дізнатись свою групу відключень";

    protected override async Task<string?> HandleCommandAsync(
        ITelegramBotClient botClient,
        Message message,
        string? parameters,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        sb.AppendLine("❓ <b>Як дізнатись свою групу (чергу) відключень?</b>");
        sb.AppendLine();
        sb.AppendLine("1️⃣ Перейдіть на сайт ДТЕК:");
        sb.AppendLine($"👉 <a href=\"{DtekWebsiteUrl}\">dtek-krem.com.ua/ua/shutdowns</a>");
        sb.AppendLine();
        sb.AppendLine("2️⃣ Введіть свою адресу:");
        sb.AppendLine("   • Населений пункт");
        sb.AppendLine("   • Вулицю");
        sb.AppendLine("   • Номер будинку");
        sb.AppendLine();
        sb.AppendLine("3️⃣ Натисніть кнопку пошуку");
        sb.AppendLine();
        sb.AppendLine("4️⃣ Ви побачите вашу чергу, наприклад:");
        sb.AppendLine("   <b>Черга 3.2</b>");
        sb.AppendLine();
        sb.AppendLine("5️⃣ Поверніться сюди та натисніть");
        sb.AppendLine("   <b>📊 Обрати групу</b>");
        sb.AppendLine("   і оберіть вашу чергу зі списку.");
        sb.AppendLine();
        sb.AppendLine("💡 Після цього ви будете отримувати сповіщення про зміни в графіку!");

        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: sb.ToString(),
            parseMode: ParseMode.Html,
            linkPreviewOptions: new Telegram.Bot.Types.LinkPreviewOptions { IsDisabled = true },
            replyMarkup: KeyboardMarkups.MainMenuKeyboard,
            cancellationToken: cancellationToken);

        return null;
    }
}

