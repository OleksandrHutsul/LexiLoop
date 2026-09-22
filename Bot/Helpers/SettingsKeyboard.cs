using Telegram.Bot.Types.ReplyMarkups;

namespace LexiLoop.Bot.Helpers;

public static class SettingsKeyboard
{
    public static InlineKeyboardMarkup Settings(bool pronunciation, bool notifications, int count, string mode, TimeOnly reviewTime) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📚 Learning pool", "settings:pool") },
        new[] { InlineKeyboardButton.WithCallbackData($"📚 New/day: {count}", "settings:new") },
        new[] { InlineKeyboardButton.WithCallbackData($"🧠 Learning mode: {mode}", "settings:mode") },
        new[] { InlineKeyboardButton.WithCallbackData($"🔊 Pronunciation: {(pronunciation ? "Yes" : "No")}", "settings:pron") },
        new[] { InlineKeyboardButton.WithCallbackData($"🔔 Notifications: {(notifications ? "On" : "Off")}", "settings:notif") },
        new[] { InlineKeyboardButton.WithCallbackData($"🕒 Review time: {reviewTime:HH\\:mm}", "settings:time") }
    });
}
