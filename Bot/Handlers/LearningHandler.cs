using System.Text;
using LexiLoop.Bot.Helpers;
using LexiLoop.Bot.Keyboards;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using LexiLoop.Services;
using LexiLoop.Services.Models;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace LexiLoop.Bot.Handlers;

public class LearningHandler
{
    private readonly ILogger<LearningHandler> _logger;
    private readonly SettingsHandler _settingsHandler;
    private readonly LearningSessionService _learningSessionService;
    private readonly StatisticsService _statisticsService;

    public LearningHandler(ILogger<LearningHandler> logger, SettingsHandler settingsHandler, LearningSessionService learningSessionService, StatisticsService statisticsService)
    {
        _logger = logger;
        _settingsHandler = settingsHandler;
        _learningSessionService = learningSessionService;
        _statisticsService = statisticsService;
    }

    public async Task HandleFreeTextAsync(ITelegramBotClient telegramBotClient, Message message, TelegramUser telegramUser,
        string text, CancellationToken cancellationToken)
    {
        var typedAnswer = await _learningSessionService.SubmitTypedAnswerAsync(telegramUser.Id, message.Chat.Id, text, cancellationToken);

        if (typedAnswer is not null)
        {
            await RemoveOldCardControlsAsync(telegramBotClient, message.Chat.Id, typedAnswer.PreviousMessageId, cancellationToken);

            var answeredCard = typedAnswer.Session.Cards
                .OrderBy(x => x.Position)
                .ElementAt(typedAnswer.Session.CurrentIndex);

            var feedback = FormatTypedAnswerFeedback(typedAnswer, answeredCard.VocabularyItem);

            await PublishTypedAnswerRatingAsync(telegramBotClient, telegramUser.Id, typedAnswer.Session, feedback, cancellationToken);

            return;
        }

        var activeSession = await _learningSessionService.GetActiveAsync(telegramUser.Id, cancellationToken);
        if (activeSession?.MessageId == 0) return;

        if (activeSession is { AnswerRevealed: true })
        {
            await telegramBotClient.SendMessage(message.Chat.Id, "The answer is already revealed. Choose Again, Hard, Good, or Easy below the card to continue.",
                cancellationToken: cancellationToken);
            return;
        }

        await telegramBotClient.SendMessage(message.Chat.Id, "Use the menu or /help to see available commands.", replyMarkup: MainMenuKeyboard.MainMenu, cancellationToken: cancellationToken);
    }

    public Task HandleRatingHelpAsync(ITelegramBotClient telegramBotClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        return telegramBotClient.AnswerCallbackQuery(callbackQuery.Id, RatingHelp, showAlert: true, cancellationToken: cancellationToken);
    }

    public async Task HandleCallbackAsync(ITelegramBotClient telegramBotClient, CallbackQuery callbackQuery, TelegramUser telegramUser,
        string[] parts, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null || parts.Length == 0) return;

        var chatId = callbackQuery.Message.Chat.Id;
        var messageId = callbackQuery.Message.Id;

        switch (parts[0])
        {
            case "choose" 
                when parts.Length == 2 && parts[1] == "review":
                await telegramBotClient.EditMessageText(chatId, messageId, "🔁 Choose a review direction",
                    replyMarkup: LearningKeyboard.LearningModes("review"), cancellationToken: cancellationToken);
                break;

            case "learn-source" 
                when parts.Length == 2 && TryParseLearningSource(parts[1], out var sourceId, out var favoriteSource):
                await SendLearningCountsAsync(telegramBotClient, chatId, messageId, telegramUser.Id, sourceId, favoriteSource, cancellationToken);
                break;

            case "collection-practice" 
                when parts.Length == 2 && CallbackDataParser.TryGetLong(parts, 1, out var practiceCollectionId):
                await SendLearningCountsAsync(telegramBotClient, chatId, messageId, telegramUser.Id, practiceCollectionId, false, cancellationToken);
                break;

            case "learn-sources" 
                when parts.Length == 2 && CallbackDataParser.TryGetInt(parts, 1, out var sourcePage):
                await SendLearnSourcesAsync(telegramBotClient, chatId, messageId, telegramUser.Id, sourcePage, cancellationToken);
                break;

            case "pool-source" 
                when parts.Length == 2 &&
                TryParseLearningSource(parts[1], out var poolSourceId, out var favoritePoolSource):
                await SendLearningCountsAsync(telegramBotClient, chatId, messageId, telegramUser.Id, poolSourceId, favoritePoolSource, cancellationToken, "pool-count");
                break;

            case "pool-sources" 
                when parts.Length == 2 && CallbackDataParser.TryGetInt(parts, 1, out var poolSourcePage):
                var poolSources = await _learningSessionService.GetLearningSourcesAsync(telegramUser.Id, cancellationToken);
                await telegramBotClient.EditMessageText(chatId, messageId, "📚 Choose vocabulary to add to your learning pool",
                    replyMarkup: LearningKeyboard.LearningSources(poolSources, "pool-source", poolSourcePage), cancellationToken: cancellationToken);
                break;

            case "learn-count" 
                when parts.Length == 3 && TryParseLearningSource(parts[1], out _, out _) && IsLearningCount(parts[2]):
                await telegramBotClient.EditMessageText(chatId, messageId, "🧠 Choose a learning direction",
                    replyMarkup: LearningKeyboard.LearningModes("learn", parts[1], parts[2]), cancellationToken: cancellationToken);
                break;

            case "pool-count" 
                when parts.Length == 3 && TryParseLearningSource(parts[1], out var poolCountSourceId, out var favoritePoolCountSource) && 
                TryParseLearningCount(parts[2], out var poolLimit):
                await _learningSessionService.AddToLearningPoolAsync(telegramUser.Id, poolCountSourceId, favoritePoolCountSource, poolLimit, parts[2] == "all", cancellationToken);
                await _settingsHandler.SendSettingsAsync(telegramBotClient, chatId, telegramUser.Id, messageId, cancellationToken);
                break;

            case "session" 
                when parts.Length is 3 or 4 or 5:
                long? sessionSourceId = null;
                var sessionFavorites = false;
                int? sessionLimit = null;
                if (parts.Length == 4 && !TryParseLearningSource(parts[3], out sessionSourceId, out sessionFavorites))
                    break;
                if (parts.Length == 5 && (!TryParseLearningSource(parts[3], out sessionSourceId, out sessionFavorites) || 
                    !TryParseLearningCount(parts[4], out sessionLimit)))
                    break;
                await StartSessionAsync(telegramBotClient, chatId, telegramUser.Id, parts[1], parts[2], messageId, cancellationToken, sessionSourceId, 
                    sessionFavorites, sessionLimit, parts.Length == 5 && parts[4] == "all");
                break;

            case "show" 
                when parts.Length == 3 &&
                Guid.TryParseExact(parts[1], "N", out var showId) &&
                CallbackDataParser.TryGetInt(parts, 2, out var showIndex):
                var shown = await _learningSessionService.RevealAsync(
                    telegramUser.Id, showId, showIndex, messageId, cancellationToken);
                if (shown is not null)
                    await RenderSessionAsync(telegramBotClient, chatId, messageId, shown, cancellationToken);
                break;

            case "answer" 
                when parts.Length == 4 && Guid.TryParseExact(parts[1], "N", out var answerId) &&
                CallbackDataParser.TryGetInt(parts, 2, out var answerIndex) && Enum.TryParse<ReviewResult>(parts[3], true, out var result):
                var session = await _learningSessionService.AnswerAsync(telegramUser.Id, answerId, answerIndex, messageId, result, cancellationToken);
                if (session is not null)
                {
                    await RemoveOldCardControlsAsync(telegramBotClient, chatId, messageId, cancellationToken);
                    await PublishAdvancedSessionAsync(telegramBotClient, telegramUser.Id, session, RatingFeedback(result), cancellationToken);
                }
                break;
        }
    }

    public Task SendLearnIntroAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        return SendLearnSourcesAsync(telegramBotClient, chatId, null, userId, 1, cancellationToken);
    }

    public async Task SendReviewIntroAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        var due = await _learningSessionService.CountDueAsync(userId, cancellationToken);

        await telegramBotClient.SendMessage(chatId, $"🔁 Review\n\nDue now: {due}",
            replyMarkup: due > 0 ? LearningKeyboard.ReviewStart : null, cancellationToken: cancellationToken);
    }

    public async Task SendTodayAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        var statistics = await _statisticsService.GetAsync(userId, cancellationToken);
        var due = await _learningSessionService.CountDueAsync(userId, cancellationToken);
        var fresh = await _learningSessionService.CountNewAsync(userId, cancellationToken);

        await telegramBotClient.SendMessage(chatId, 
            $"📅 Today\n\n{due} items to review\n{fresh} new items available\nCurrent streak: 🔥 {statistics.CurrentStreak} days",
            replyMarkup: due + fresh > 0 ? LearningKeyboard.TodayStart : null, cancellationToken: cancellationToken);
    }

    private async Task StartSessionAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, string kindText, string modeText, int messageId, 
        CancellationToken cancellationToken, long? collectionId = null, bool favorites = false, int? learnItemLimit = null, bool allLearnItems = false)
    {
        var kind = kindText switch
        {
            "review" => LearningSessionKind.Review,
            "today" => LearningSessionKind.Today,
            _ => LearningSessionKind.Learn
        };

        var mode = modeText switch
        {
            "f2t" => LearningMode.ForeignToTranslation,
            "t2f" => LearningMode.TranslationToForeign,
            _ => LearningMode.Mixed
        };

        var session = await _learningSessionService.CreateAsync(userId, kind, mode, chatId, messageId, cancellationToken, collectionId, favorites, 
            learnItemLimit, allLearnItems);

        if (session is null)
        {
            var text = kind == LearningSessionKind.Review
                ? "🎉 Nothing is due right now."
                : "🎉 You're done for now!\n\nNo cards are currently due for review. Next reviews will become available later.";

            await telegramBotClient.EditMessageText(chatId, messageId, text, cancellationToken: cancellationToken);
            return;
        }

        await RenderSessionAsync(telegramBotClient, chatId, messageId, session, cancellationToken);
    }

    private async Task SendLearnSourcesAsync(ITelegramBotClient telegramBotClient, long chatId, int? messageId, long userId, int page, CancellationToken cancellationToken)
    {
        var sources = await _learningSessionService.GetLearningSourcesAsync(userId, cancellationToken);
        const string text = "🧠 <b>Learn</b>\n\nWhat do you want to learn?";
        var keyboard = LearningKeyboard.LearningSources(sources, page: page);

        if (messageId is null)
        {
            await telegramBotClient.SendMessage(chatId, text, ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
            return;
        }

        await telegramBotClient.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private async Task SendLearningCountsAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId,
        long? collectionId, bool favorites, CancellationToken cancellationToken, string callbackPrefix = "learn-count")
    {
        var available = await _learningSessionService.CountLearnableAsync(userId, collectionId, favorites, cancellationToken);

        if (available == 0)
        {
            var text = favorites
                ? "You do not have any favorite vocabulary yet."
                : collectionId is null
                    ? "No vocabulary is available yet. Add some with /add."
                    : "No vocabulary is available in this list.";

            var backCallback = callbackPrefix == "pool-count"
                ? "pool-sources:1"
                : "learn-sources:1";

            await telegramBotClient.EditMessageText(chatId, messageId, text, replyMarkup: LearningKeyboard.LearningSourcesBack(backCallback), cancellationToken: cancellationToken);
            return;
        }

        var sourceKey = favorites ? "favorites" : collectionId?.ToString() ?? "all";

        await telegramBotClient.EditMessageText(chatId, messageId, "🧠 How many items do you want to make available for learning?",
            replyMarkup: LearningKeyboard.LearningWordCounts(sourceKey, available, callbackPrefix), cancellationToken: cancellationToken);
    }

    private static bool TryParseLearningSource(string value, out long? collectionId, out bool favorites)
    {
        collectionId = null;
        favorites = value == "favorites";

        if (favorites || value == "all") return true;
        if (!long.TryParse(value, out var parsed) || parsed <= 0) return false;

        collectionId = parsed;
        return true;
    }

    private static bool IsLearningCount(string value)
    {
        return value == "all" || value is "10" or "20" or "50" or "100";
    }

    private static bool TryParseLearningCount(string value, out int? limit)
    {
        limit = null;

        if (value == "all") return true;
        if (!IsLearningCount(value) || !int.TryParse(value, out var parsed)) return false;

        limit = parsed;
        return true;
    }

    private static async Task RenderSessionAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId,
        LearningSession session, CancellationToken cancellationToken)
    {
        if (session.Status == LearningSessionStatus.Completed)
        {
            var pages = FormatSessionSummaryPages(session);

            await telegramBotClient.EditMessageText(chatId, messageId, pages[0], ParseMode.Html,
                replyMarkup: pages.Count == 1 ? LearningKeyboard.SessionComplete : null, cancellationToken: cancellationToken);

            for (var i = 1; i < pages.Count; i++)
            {
                await telegramBotClient.SendMessage(chatId, pages[i], ParseMode.Html,
                    replyMarkup: i == pages.Count - 1 ? LearningKeyboard.SessionComplete : null, cancellationToken: cancellationToken);
            }

            return;
        }

        var card = session.Cards.OrderBy(x => x.Position).ElementAt(session.CurrentIndex);
        var progress = $"{session.CurrentIndex + 1} / {session.Cards.Count}";

        if (!session.AnswerRevealed)
        {
            await telegramBotClient.EditMessageText(chatId, messageId,$"{FormatPrompt(card)}\n\n{progress}", ParseMode.Html,
                replyMarkup: LearningKeyboard.ShowAnswer(session.Id, session.CurrentIndex), cancellationToken: cancellationToken);
            return;
        }

        await telegramBotClient.EditMessageText(chatId, messageId, $"{FormatCompleteCard(card.VocabularyItem)}\n\n{progress}\n\nHow well did you know it?", ParseMode.Html,
            replyMarkup: LearningKeyboard.Answers(session.Id, session.CurrentIndex), cancellationToken: cancellationToken);
    }

    private async Task PublishTypedAnswerRatingAsync(ITelegramBotClient telegramBotClient, long userId,
        LearningSession session, string feedback, CancellationToken cancellationToken)
    {
        var progress = $"{session.CurrentIndex + 1} / {session.Cards.Count}";
        var text = $"{feedback}\n\n{progress}\n\nHow well did you know it?";

        var sent = await telegramBotClient.SendMessage(session.ChatId, text, ParseMode.Html,
            replyMarkup: LearningKeyboard.Answers(session.Id, session.CurrentIndex), cancellationToken: cancellationToken);

        if (await _learningSessionService.UpdateMessageIdAsync(userId, session.Id, session.CurrentIndex, sent.Id, cancellationToken)) return;

        _logger.LogWarning("Could not attach Telegram rating message {MessageId} to learning session {SessionId}", sent.Id, session.Id);

        await RemoveOldCardControlsAsync(telegramBotClient, session.ChatId, sent.Id, cancellationToken);
    }

    private async Task PublishAdvancedSessionAsync(ITelegramBotClient telegramBotClient, long userId,
        LearningSession session, string feedback, CancellationToken cancellationToken)
    {
        if (session.Status == LearningSessionStatus.Completed)
        {
            var pages = FormatSessionSummaryPages(session);
            Message? lastMessage = null;

            for (var i = 0; i < pages.Count; i++)
            {
                lastMessage = await telegramBotClient.SendMessage(session.ChatId, pages[i], ParseMode.Html, 
                    replyMarkup: i == pages.Count - 1 ? LearningKeyboard.SessionComplete : null, cancellationToken: cancellationToken);
            }

            if (lastMessage is null || await _learningSessionService.UpdateMessageIdAsync(userId, session.Id, session.CurrentIndex, lastMessage.Id, cancellationToken))
                return;

            _logger.LogWarning("Could not attach Telegram summary message {MessageId} to learning session {SessionId}", lastMessage.Id, session.Id);

            await RemoveOldCardControlsAsync(telegramBotClient, session.ChatId, lastMessage.Id, cancellationToken);
            return;
        }

        var card = session.Cards.OrderBy(x => x.Position).ElementAt(session.CurrentIndex);
        var progress = $"{session.CurrentIndex + 1} / {session.Cards.Count}";
        var text = $"{feedback}\n\n<b>Next:</b>\n\n{FormatPrompt(card)}\n\n{progress}";

        var sent = await telegramBotClient.SendMessage(session.ChatId, text, ParseMode.Html, 
            replyMarkup: LearningKeyboard.ShowAnswer(session.Id, session.CurrentIndex), cancellationToken: cancellationToken);

        if (await _learningSessionService.UpdateMessageIdAsync(userId, session.Id, session.CurrentIndex, sent.Id, cancellationToken)) return;

        _logger.LogWarning("Could not attach Telegram message {MessageId} to learning session {SessionId}", sent.Id, session.Id);

        await RemoveOldCardControlsAsync(telegramBotClient, session.ChatId, sent.Id, cancellationToken);
    }

    private async Task RemoveOldCardControlsAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, CancellationToken cancellationToken)
    {
        if (messageId == 0) return;

        try
        {
            await telegramBotClient.EditMessageReplyMarkup(chatId, messageId, replyMarkup: null, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException exception)
        {
            _logger.LogDebug(exception, "Could not remove controls from old learning message {MessageId}", messageId);
        }
    }

    private static string RatingFeedback(ReviewResult result) => result switch
    {
        ReviewResult.Again => "❌ <b>Again</b>",
        ReviewResult.Hard => "😐 <b>Hard</b>",
        ReviewResult.Easy => "😎 <b>Easy</b>",
        _ => "✅ <b>Good</b>"
    };

    private static string FormatSessionSummary(LearningSession session)
    {
        var results = session.Cards
            .Where(x => x.Completed && x.Result is not null)
            .Select(x => x.Result!.Value)
            .ToList();

        var again = results.Count(x => x == ReviewResult.Again);
        var hard = results.Count(x => x == ReviewResult.Hard);
        var good = results.Count(x => x == ReviewResult.Good);
        var easy = results.Count(x => x == ReviewResult.Easy);
        var reviewed = results.Count;
        var incorrect = again;
        var correct = reviewed - incorrect;
        var accuracy = reviewed == 0 ? 0 : correct * 100d / reviewed;
        var accuracyText = accuracy.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        return $"🎉 <b>Session complete</b>\n\n📚 {TelegramHtml.Encode(session.SourceName)}\n🧠 Reviewed: {reviewed}\n" +
            $"✅ Correct: {correct}\n❌ Incorrect: {incorrect}\n🎯 Accuracy: {accuracyText}%\n\n Again: {again}\nHard: {hard}\nGood: {good}\nEasy: {easy}";
    }

    private static IReadOnlyList<string> FormatSessionSummaryPages(LearningSession session)
    {
        const int pageLength = 3600;

        var mistakeGroups = session.Cards
            .Where(x => x.TypedAnswerCorrect == false && x.EnteredAnswer is not null && x.ExpectedAnswer is not null)
            .OrderBy(x => x.Position)
            .GroupBy(x => x.VocabularyItemId)
            .ToList();

        if (mistakeGroups.Count == 0)
            return [FormatSessionSummary(session)];

        var pages = new List<string>();
        var current = new StringBuilder(FormatSessionSummary(session));

        current.Append("\n\n❌ <b>Words to review</b>");

        for (var i = 0; i < mistakeGroups.Count; i++)
        {
            var block = FormatMistake(i + 1, mistakeGroups[i]);

            if (current.Length + block.Length > pageLength && current.Length > 0)
            {
                pages.Add(current.ToString());
                current = new StringBuilder("❌ <b>Words to review (continued)</b>");
            }

            current.Append(block);
        }

        pages.Add(current.ToString());
        return pages;
    }

    private static string FormatMistake(int number, IGrouping<long, LearningSessionCard> mistakes)
    {
        var attempts = mistakes.ToList();
        var item = attempts[0].VocabularyItem;
        var count = attempts.Count;

        var text = new StringBuilder(
            $"\n\n{number}. <b>{SummaryValue(item.ForeignText)}</b> — {SummaryValue(item.Translation)}");

        if (count > 1)
            text.Append($" — <b>{count} mistakes</b>");

        if (!string.IsNullOrWhiteSpace(item.Pronunciation))
            text.Append($"\n   🔊 {SummaryValue(item.Pronunciation)}");

        for (var i = 0; i < attempts.Count; i++)
        {
            var attempt = attempts[i];

            if (attempts.Count > 1)
                text.Append($"\n   Attempt {i + 1} ({DirectionLabel(attempt.Direction)}):");
            else
                text.Append($"\n   Direction: {DirectionLabel(attempt.Direction)}");

            text.Append($"\n   Expected answer: {SummaryValue(attempt.ExpectedAnswer!)}");
            text.Append($"\n   Your answer: {SummaryValue(attempt.EnteredAnswer!)}");
        }

        return text.ToString();
    }

    private static string DirectionLabel(ReviewDirection direction)
    {
        return direction == ReviewDirection.ForeignToTranslation
            ? "Foreign → Translation"
            : "Translation → Foreign";
    }

    private static string SummaryValue(string value)
    {
        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length > 250)
            normalized = normalized[..249] + "…";

        return TelegramHtml.Encode(normalized);
    }

    private static string FormatPrompt(LearningSessionCard card)
    {
        return card.Direction == ReviewDirection.ForeignToTranslation
            ? $"🧠 <b>Translate:</b>\n\n🌐 {TelegramHtml.Encode(card.VocabularyItem.ForeignText)}"
            : $"🧠 <b>Recall the foreign text:</b>\n\n💬 {TelegramHtml.Encode(card.VocabularyItem.Translation)}";
    }

    private static string FormatCompleteCard(VocabularyItem item)
    {
        return $"🌐 {TelegramHtml.Encode(item.ForeignText)}\n💬 {TelegramHtml.Encode(item.Translation)}{VocabularyHandler.FormatPronunciation(item)}";
    }

    private static string FormatTypedAnswerFeedback(TypedAnswerResult answer, VocabularyItem item)
    {
        return answer.IsCorrect
            ? $"✅ <b>Correct!</b>\n\nCorrect answer: <b>{TelegramHtml.Encode(answer.ExpectedAnswer)}</b>{VocabularyHandler.FormatPronunciation(item)}"
            : $"❌ <b>Not quite</b>\n\nYour answer: {TelegramHtml.Encode(answer.EnteredAnswer)}\nCorrect answer: <b>{TelegramHtml.Encode(answer.ExpectedAnswer)}</b>{VocabularyHandler.FormatPronunciation(item)}";
    }

    private const string RatingHelp = """
        ❌ Again — didn't know; retry soon.
        😐 Hard — recalled with difficulty; shorter interval.
        ✅ Good — knew it; normal interval.
        😎 Easy — knew immediately; longer interval.
        """;
}
