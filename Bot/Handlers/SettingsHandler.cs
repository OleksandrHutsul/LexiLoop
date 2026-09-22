using LexiLoop.Bot.Helpers;
using LexiLoop.Bot.Keyboards;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using LexiLoop.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace LexiLoop.Bot.Handlers;

public class SettingsHandler
{
    private readonly UserSettingsService _userSettingsService;
    private readonly LearningSessionService _learningSessionService;
    private readonly UserService _userService;

    public SettingsHandler(UserSettingsService userSettingsService, LearningSessionService learningSessionService, UserService userService)
    {
        _userSettingsService = userSettingsService;
        _learningSessionService = learningSessionService;
        _userService = userService;
    }

    public async Task SendSettingsAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, int? messageId, CancellationToken cancellationToken)
    {
        var settings = await _userSettingsService.GetAsync(userId, cancellationToken);
        var poolCount = await _learningSessionService.CountLearningPoolAsync(userId, cancellationToken);
        var mode = LearningModeLabel(settings.DefaultLearningMode);

        var text = $"⚙️ Settings\n\n" +
            $"📚 Learning pool: {poolCount} items enabled\n" +
            $"📚 New items per day: {settings.NewItemsPerDay}\n" +
            $"🧠 Learning mode: {mode}\n" +
            $"🔔 Review notifications: {(settings.ReviewNotificationsEnabled ? "On" : "Off")}\n" +
            $"🕒 Review time: {settings.ReviewNotificationTime:HH\\:mm}\n" +
            $"🔊 Show pronunciation: {(settings.ShowPronunciation ? "Yes" : "No")}";

        var keyboard = SettingsKeyboard.Settings(settings.ShowPronunciation, settings.ReviewNotificationsEnabled, settings.NewItemsPerDay, mode, settings.ReviewNotificationTime);

        if (messageId is null)
        {
            await telegramBotClient.SendMessage(chatId, text, replyMarkup: keyboard, cancellationToken: cancellationToken);
            return;
        }

        await telegramBotClient.EditMessageText(chatId, messageId.Value, text, replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    public async Task HandleCallbackAsync(ITelegramBotClient telegramBotClient, CallbackQuery callbackQuery, TelegramUser telegramUser, string[] parts, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null) return;

        await HandleSettingsCallbackAsync(telegramBotClient, callbackQuery.Message.Chat.Id, callbackQuery.Message.Id, telegramUser.Id, parts, cancellationToken);
    }

    public async Task HandleSettingsInputAsync(ITelegramBotClient telegramBotClient, long chatId, TelegramUser telegramUser, string text, CancellationToken cancellationToken)
    {
        if (telegramUser.InputMode == UserInputMode.SettingsNewItemsPerDay)
        {
            if (!SettingsInputParser.TryParseNewItemsPerDay(text, out var value))
            {
                await telegramBotClient.SendMessage(chatId, "❌ Please enter a positive whole number.\n\nUse /cancel to keep the current value.", cancellationToken: cancellationToken);
                return;
            }

            await _userSettingsService.SetNewItemsPerDayAsync(telegramUser.Id, value, cancellationToken);
            await _userService.SetInputModeAsync(telegramUser.Id, UserInputMode.None, cancellationToken);

            await telegramBotClient.SendMessage(chatId, $"✅ New items per day updated to {value}.", cancellationToken: cancellationToken);
        }
        else
        {
            if (!SettingsInputParser.TryParseReviewTime(text, out var value))
            {
                await telegramBotClient.SendMessage(chatId,
                    "❌ Invalid time.\n\nPlease use 24-hour format, for example:\n08:30\n19:00\n21:15\n\nUse /cancel to keep the current value.", cancellationToken: cancellationToken);
                return;
            }

            await _userSettingsService.SetReviewTimeAsync(telegramUser.Id, value, cancellationToken);
            await _userService.SetInputModeAsync(telegramUser.Id, UserInputMode.None, cancellationToken);

            await telegramBotClient.SendMessage(chatId, $"✅ Review notification time updated to {value:HH\\:mm}.", cancellationToken: cancellationToken);
        }

        await SendSettingsAsync(telegramBotClient, chatId, telegramUser.Id, null, cancellationToken);
    }

    private async Task HandleSettingsCallbackAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, string[] parts, CancellationToken cancellationToken)
    {
        if (parts.Length < 2) return;

        var action = parts[1];
        if (action == "pool")
        {
            await SendLearningPoolAsync(telegramBotClient, chatId, messageId, userId, cancellationToken);
            return;
        }

        var settings = await _userSettingsService.GetAsync(userId, cancellationToken);

        if (action == "new")
        {
            await _userService.SetInputModeAsync(userId, UserInputMode.SettingsNewItemsPerDay, cancellationToken);

            await telegramBotClient.SendMessage(chatId, $"📚 <b>New items per day</b>\n\n" +
                $"Current value: {settings.NewItemsPerDay}\n\n" +
                "Send the number of new vocabulary items you want to learn per day.\n\n" +
                "Example: <code>20</code>\n" +
                "Enter any positive whole number.\n\n" +
                "Use /cancel to keep the current value.",
                ParseMode.Html, cancellationToken: cancellationToken);
            return;
        }

        if (action == "time")
        {
            await _userService.SetInputModeAsync(userId, UserInputMode.SettingsReviewNotificationTime, cancellationToken);

            await telegramBotClient.SendMessage(chatId, $"🕒 <b>Review notification time</b>\n\n" +
                $"Current time: {settings.ReviewNotificationTime:HH\\:mm}\n\n" +
                "Send the time when you would like to receive review reminders.\n" +
                "Use 24-hour format: <code>HH:mm</code>\n\n" +
                "Examples: <code>08:30</code>, <code>14:00</code>, <code>21:15</code>\n\n" +
                "Use /cancel to keep the current value.",
                ParseMode.Html, cancellationToken: cancellationToken);
            return;
        }

        await _userService.SetInputModeAsync(userId, UserInputMode.None, cancellationToken);
        await _userSettingsService.UpdateButtonSettingAsync(userId, action, cancellationToken);
        await SendSettingsAsync(telegramBotClient, chatId, userId, messageId, cancellationToken);
    }

    private async Task SendLearningPoolAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, CancellationToken cancellationToken)
    {
        var sources = await _learningSessionService.GetLearningSourcesAsync(userId, cancellationToken);

        if (sources.Count == 0 || sources[0].AvailableCount == 0)
        {
            await telegramBotClient.EditMessageText(chatId, messageId, "Add some vocabulary first with /add.", cancellationToken: cancellationToken);
            return;
        }

        await telegramBotClient.EditMessageText(chatId, messageId, "📚 Choose vocabulary to add to your learning pool", 
            replyMarkup: LearningKeyboard.LearningSources(sources, "pool-source"), cancellationToken: cancellationToken);
    }

    private static string LearningModeLabel(LearningMode mode) => mode switch
    {
        LearningMode.ForeignToTranslation => "Foreign → Translation",
        LearningMode.TranslationToForeign => "Translation → Foreign",
        _ => "Mixed"
    };
}
