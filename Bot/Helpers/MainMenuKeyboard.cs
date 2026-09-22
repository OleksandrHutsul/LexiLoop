using Telegram.Bot.Types.ReplyMarkups;

namespace LexiLoop.Bot.Helpers;

public static class MainMenuKeyboard
{
    public static ReplyKeyboardMarkup MainMenu => new(new[]
    {
        new[] { new KeyboardButton("➕ Add"), new KeyboardButton("📥 Import") },
        new[] { new KeyboardButton("📚 Vocabulary"), new KeyboardButton("📁 Collections") },
        new[] { new KeyboardButton("🧠 Learn"), new KeyboardButton("🔁 Review") },
        new[] { new KeyboardButton("📊 Statistics"), new KeyboardButton("⚙️ Settings") },
        new[] { new KeyboardButton("❓ Help") }
    }) { ResizeKeyboard = true };
}
