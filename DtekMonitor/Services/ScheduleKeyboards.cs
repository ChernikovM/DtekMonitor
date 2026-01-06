using DtekMonitor.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace DtekMonitor.Services;

/// <summary>
/// Helper class for creating inline keyboards for schedule and group selection
/// </summary>
public static class ScheduleKeyboards
{
    // Callback data prefixes - used by DtekCallbackMiddleware
    public const string SetGroupPrefix = "setgroup:";
    public const string SchedulePrefix = "schedule:";

    /// <summary>
    /// Creates inline keyboard for schedule navigation (Today/Tomorrow)
    /// </summary>
    public static InlineKeyboardMarkup CreateScheduleKeyboard(string groupName, string currentDay, bool tomorrowAvailable)
    {
        var todayButton = currentDay == "today"
            ? InlineKeyboardButton.WithCallbackData("📅 Сьогодні ✓", $"{SchedulePrefix}{groupName}:today")
            : InlineKeyboardButton.WithCallbackData("📅 Сьогодні", $"{SchedulePrefix}{groupName}:today");

        var tomorrowText = tomorrowAvailable ? "📆 Завтра" : "📆 Завтра (немає)";
        var tomorrowButton = currentDay == "tomorrow"
            ? InlineKeyboardButton.WithCallbackData($"{tomorrowText} ✓", $"{SchedulePrefix}{groupName}:tomorrow")
            : InlineKeyboardButton.WithCallbackData(tomorrowText, $"{SchedulePrefix}{groupName}:tomorrow");

        return new InlineKeyboardMarkup(new[]
        {
            new[] { todayButton, tomorrowButton }
        });
    }

    /// <summary>
    /// Creates inline keyboard for group selection (using display names like "1.1", "3.2")
    /// </summary>
    public static InlineKeyboardMarkup CreateGroupSelectionKeyboard()
    {
        var buttons = new List<InlineKeyboardButton[]>();

        // Create 2 buttons per row using display names
        for (int i = 0; i < DtekGroups.DisplayGroups.Length; i += 2)
        {
            var displayName1 = DtekGroups.DisplayGroups[i];
            var apiName1 = DtekGroups.ApiGroups[i];
            
            var row = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"Черга {displayName1}", $"{SetGroupPrefix}{apiName1}")
            };

            if (i + 1 < DtekGroups.DisplayGroups.Length)
            {
                var displayName2 = DtekGroups.DisplayGroups[i + 1];
                var apiName2 = DtekGroups.ApiGroups[i + 1];
                row.Add(InlineKeyboardButton.WithCallbackData($"Черга {displayName2}", $"{SetGroupPrefix}{apiName2}"));
            }

            buttons.Add(row.ToArray());
        }

        return new InlineKeyboardMarkup(buttons);
    }
}


