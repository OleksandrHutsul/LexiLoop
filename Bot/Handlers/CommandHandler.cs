using LexiLoop.Bot.Helpers;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using LexiLoop.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace LexiLoop.Bot.Handlers;

public class CommandHandler
{
    private readonly ImportHandler _importHandler;
    private readonly LearningHandler _learningHandler;
    private readonly VocabularyHandler _vocabularyHandler;
    private readonly CollectionHandler _collectionHandler;
    private readonly CollectionSharingHandler _collectionSharingHandler;
    private readonly SettingsHandler _settingsHandler;
    private readonly UserService _userService;
    private readonly StatisticsService _statisticsService;

    public CommandHandler(ImportHandler importHandler, LearningHandler learningHandler, VocabularyHandler vocabularyHandler, CollectionHandler collectionHandler,
        CollectionSharingHandler collectionSharingHandler, SettingsHandler settingsHandler, UserService userService, StatisticsService statisticsService)
    {
        _importHandler = importHandler;
        _learningHandler = learningHandler;
        _vocabularyHandler = vocabularyHandler;
        _collectionHandler = collectionHandler;
        _collectionSharingHandler = collectionSharingHandler;
        _settingsHandler = settingsHandler;
        _userService = userService;
        _statisticsService = statisticsService;
    }

    public static bool TryGetCommand(string text, out string command, out string fullText)
    {
        command = text.Split(' ', 2)[0].Split('@')[0].ToLowerInvariant();
        fullText = text;

        var menu = MenuCommand(text);
        if (!command.StartsWith('/') && menu is null) return false;

        if (menu is not null)
        {
            command = menu;
            fullText = menu;
        }

        return true;
    }

    public async Task HandleAsync(ITelegramBotClient telegramBotClient, long chatId, TelegramUser telegramUser, string command, string fullText, CancellationToken cancellationToken)
    {
        if (command is not "/add" and not "/import")
        {
            _importHandler.ClearPending(telegramUser.Id);
            _collectionHandler.ClearPending(telegramUser.Id);
            await _userService.SetInputModeAsync(telegramUser.Id, UserInputMode.None, cancellationToken);
        }

        switch (command)
        {
            case "/start":
                if (TryGetShareToken(fullText, out var startShareToken))
                    await _collectionSharingHandler.SendSharedCollectionAsync(telegramBotClient, chatId, null, telegramUser.Id, startShareToken, 1, false, cancellationToken);
                else
                    await telegramBotClient.SendMessage(chatId, WelcomeText(telegramUser.FirstName), parseMode: ParseMode.Html, 
                        replyMarkup: MainMenuKeyboard.MainMenu, cancellationToken: cancellationToken);
                break;

            case "/add":
                await _importHandler.StartAddAsync(telegramBotClient, chatId, telegramUser.Id, cancellationToken);
                break;

            case "/import":
                await _importHandler.StartImportAsync(telegramBotClient, chatId, telegramUser.Id, cancellationToken);
                break;

            case "/cancel":
                _importHandler.ClearPending(telegramUser.Id);
                _collectionHandler.ClearPending(telegramUser.Id);
                await _userService.SetInputModeAsync(telegramUser.Id, UserInputMode.None, cancellationToken);
                var cancellationText = telegramUser.InputMode is UserInputMode.SettingsNewItemsPerDay or UserInputMode.SettingsReviewNotificationTime
                    ? "Cancelled. Your current setting was kept."
                    : "Cancelled.";
                await telegramBotClient.SendMessage(chatId, cancellationText, replyMarkup: MainMenuKeyboard.MainMenu, cancellationToken: cancellationToken);
                break;

            case "/list":
                await _vocabularyHandler.SendListAsync(telegramBotClient, chatId, telegramUser.Id, "all", 1, null, cancellationToken);
                break;

            case "/learn":
                await _learningHandler.SendLearnIntroAsync(telegramBotClient, chatId, telegramUser.Id, cancellationToken);
                break;

            case "/review":
                await _learningHandler.SendReviewIntroAsync(telegramBotClient, chatId, telegramUser.Id, cancellationToken);
                break;

            case "/today":
                await _learningHandler.SendTodayAsync(telegramBotClient, chatId, telegramUser.Id, cancellationToken);
                break;

            case "/stats":
                await SendStatsAsync(telegramBotClient, chatId, telegramUser.Id, cancellationToken);
                break;

            case "/settings":
                await _settingsHandler.SendSettingsAsync(telegramBotClient, chatId, telegramUser.Id, null, cancellationToken);
                break;

            case "/collections":
                await _collectionHandler.SendCollectionsAsync(telegramBotClient, chatId, telegramUser.Id, fullText, cancellationToken);
                break;

            case "/help":
                await telegramBotClient.SendMessage(chatId, HelpText, replyMarkup: MainMenuKeyboard.MainMenu, cancellationToken: cancellationToken);
                break;

            default:
                await telegramBotClient.SendMessage(chatId, "Unknown command. Use /help.", cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task SendStatsAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        var statistics = await _statisticsService.GetAsync(userId, cancellationToken);

        var text = $"📊 Vocabulary statistics\n\n" +
            $"Total items: {statistics.Total}\n" +
            $"Words: {statistics.Words}\n" +
            $"Phrases: {statistics.Phrases}\n\n" +
            $"New: {statistics.New}\n" +
            $"Learning: {statistics.Learning}\n" +
            $"Learned: {statistics.Learned}\n\n" +
            $"Due now: {statistics.Due}\n" +
            $"Reviews today: {statistics.ReviewsToday}\n" +
            $"Correct answers: {statistics.CorrectPercent}%\n\n" +
            $"🔥 Current streak: {statistics.CurrentStreak} days\n" +
            $"🏆 Longest streak: {statistics.LongestStreak} days\n\n" +
            $"Added this week: {statistics.AddedThisWeek}\n" +
            $"Learned this week: {statistics.LearnedThisWeek}\n" +
            $"Reviews this week: {statistics.ReviewsThisWeek}";

        await telegramBotClient.SendMessage(chatId, text, cancellationToken: cancellationToken);
    }

    private static bool TryGetShareToken(string fullText, out string token)
    {
        token = "";

        var parts = fullText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !parts[1].StartsWith("share_", StringComparison.Ordinal)) return false;

        token = parts[1]["share_".Length..];
        return token.Length > 0;
    }

    private static string? MenuCommand(string text) => text switch
    {
        "📚 Vocabulary" => "/list",
        "📁 Collections" or "📁 Lists" => "/collections",
        "➕ Add" => "/add",
        "📥 Import" => "/import",
        "🧠 Learn" => "/learn",
        "🔁 Review" => "/review",
        "📊 Statistics" => "/stats",
        "⚙️ Settings" => "/settings",
        "❓ Help" => "/help",
        _ => null
    };

    private static string WelcomeText(string? firstName) => $"""
        👋 <b>Welcome to LexiLoop, {TelegramHtml.Encode(firstName ?? "learner")}!</b>

        🧠 Build your own vocabulary and remember it with spaced repetition.

        ➕ Add words and phrases
        📥 Import many items from CSV or JSON
        📚 Browse your vocabulary
        🧠 Learn new items
        🔁 Review them at the right time
        📊 Track your progress

        Start with /add or /import, then use /learn.
        Choose an action below to get started.
        """;

    private const string HelpText = """
        LexiLoop commands

        /today — daily session
        /add — add line-based vocabulary
        /import — import CSV, TSV, or JSON
        /list — browse and filter vocabulary
        /learn — learn new items
        /review — review due items
        /collections [name] — manage vocabulary lists; open a list to rename, delete, or edit its items
        /stats — learning statistics
        /settings — learning preferences
        /cancel — cancel text input
        /help — this message

        During a learning card, type your answer for an exact check (case and extra whitespace are ignored), or use Show answer for self-review.
        """;
}
