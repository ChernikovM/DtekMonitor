using System.Text;
using DtekMonitor.Commands.Abstractions;
using DtekMonitor.Models;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

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

    protected override Task<string?> HandleCommandAsync(
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
        sb.AppendLine("📋 <b>Доступні команди:</b>");
        sb.AppendLine("/setgroup [ГРУПА] - Підписатися на групу (напр. /setgroup GPV4.1)");
        sb.AppendLine("/mygroup - Показати вашу поточну групу підписки");
        sb.AppendLine("/schedule - Показати поточний графік для вашої групи");
        sb.AppendLine("/stop - Відписатися від сповіщень");
        sb.AppendLine();
        sb.AppendLine("📊 <b>Доступні групи:</b>");
        sb.AppendLine($"<code>{string.Join(", ", DtekGroups.AllGroups)}</code>");
        sb.AppendLine();
        sb.AppendLine("💡 Щоб почати, введіть команду /setgroup з номером вашої групи.");

        return Task.FromResult<string?>(sb.ToString());
    }
}


