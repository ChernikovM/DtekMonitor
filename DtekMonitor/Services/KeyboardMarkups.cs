using Telegram.Bot.Types.ReplyMarkups;

namespace DtekMonitor.Services;

/// <summary>
/// Static keyboard markups for the bot
/// </summary>
public static class KeyboardMarkups
{
    /// <summary>
    /// Main menu keyboard - always visible at the bottom of the chat
    /// </summary>
    public static ReplyKeyboardMarkup MainMenuKeyboard => new(new[]
    {
        new KeyboardButton[] { "📅 Розклад", "📊 Обрати групу" },
        new KeyboardButton[] { "ℹ️ Моя група", "❓ Як дізнатись групу" }
    })
    {
        ResizeKeyboard = true,  // Fit buttons to their text
        IsPersistent = true     // Always show keyboard
    };

    /// <summary>
    /// Keyboard to hide/remove the reply keyboard
    /// </summary>
    public static ReplyKeyboardRemove RemoveKeyboard => new();
}

