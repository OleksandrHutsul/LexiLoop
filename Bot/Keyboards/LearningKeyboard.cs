using LexiLoop.Bot.Helpers;
using LexiLoop.Services.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace LexiLoop.Bot.Keyboards;

public static class LearningKeyboard
{
    public static InlineKeyboardMarkup LearningModes(string kind, string? sourceKey = null, string? count = null)
    {
        var suffix = "";

        if (sourceKey is not null)
            suffix = count is null ? $":{sourceKey}" : $":{sourceKey}:{count}";

        return new([
            [InlineKeyboardButton.WithCallbackData("Foreign → Translation", $"session:{kind}:f2t{suffix}")],
            [InlineKeyboardButton.WithCallbackData("Translation → Foreign", $"session:{kind}:t2f{suffix}")],
            [InlineKeyboardButton.WithCallbackData("Mixed", $"session:{kind}:mixed{suffix}")]
        ]);
    }

    public static InlineKeyboardMarkup LearningSources(IReadOnlyList<LearningSource> sources, string callbackPrefix = "learn-source", int page = 1, int pageSize = 8)
    {
        var systemSources = new List<LearningSource>();
        var collections = new List<LearningSource>();

        foreach (var source in sources)
        {
            if (source.IsSystem)
                systemSources.Add(source);
            else
                collections.Add(source);
        }

        var pageCount = Math.Max(1, (int)Math.Ceiling(collections.Count / (double)pageSize));
        page = Math.Clamp(page, 1, pageCount);

        var rows = new List<InlineKeyboardButton[]>();

        foreach (var source in systemSources)
        {
            rows.Add([InlineKeyboardButton.WithCallbackData($"{KeyboardText.TrimButtonText(source.Name)} — {source.AvailableCount}", $"{callbackPrefix}:{source.Key}")]);
        }

        var startIndex = (page - 1) * pageSize;
        var endIndex = Math.Min(startIndex + pageSize, collections.Count);

        for (var i = startIndex; i < endIndex; i++)
        {
            var source = collections[i];

            rows.Add([InlineKeyboardButton.WithCallbackData($"{KeyboardText.TrimButtonText(source.Name)} — {source.AvailableCount}", $"{callbackPrefix}:{source.Key}")]);
        }

        if (pageCount > 1)
        {
            var pageCallbackPrefix = callbackPrefix == "pool-source" ? "pool-sources" : "learn-sources";
            var nav = new List<InlineKeyboardButton>();

            if (page > 1)
                nav.Add(InlineKeyboardButton.WithCallbackData("◀", $"{pageCallbackPrefix}:{page - 1}"));

            nav.Add(InlineKeyboardButton.WithCallbackData($"{page} / {pageCount}", "noop"));

            if (page < pageCount)
                nav.Add(InlineKeyboardButton.WithCallbackData("More lists ▶", $"{pageCallbackPrefix}:{page + 1}"));

            rows.Add(nav.ToArray());
        }

        return new(rows);
    }

    public static InlineKeyboardMarkup LearningSourcesBack(string callbackData)
    {
        return new(InlineKeyboardButton.WithCallbackData("◀ Choose another list", callbackData));
    }

    public static InlineKeyboardMarkup LearningWordCounts(string sourceKey, int availableCount, string callbackPrefix = "learn-count")
    {
        var counts = new[] { 10, 20, 50, 100 };
        var buttons = new List<InlineKeyboardButton>();

        foreach (var count in counts)
        {
            if (count <= availableCount)
                buttons.Add(InlineKeyboardButton.WithCallbackData(count.ToString(), $"{callbackPrefix}:{sourceKey}:{count}"));
        }

        buttons.Add(InlineKeyboardButton.WithCallbackData($"All ({availableCount})", $"{callbackPrefix}:{sourceKey}:all"));

        var rows = new List<InlineKeyboardButton[]>();

        for (var i = 0; i < buttons.Count; i += 2)
        {
            if (i + 1 < buttons.Count)
                rows.Add([buttons[i], buttons[i + 1]]);
            else
                rows.Add([buttons[i]]);
        }

        var backCallback = callbackPrefix == "pool-count" ? "pool-sources:1" : "learn-sources:1";
        rows.Add([InlineKeyboardButton.WithCallbackData("◀ Choose another list", backCallback)]);

        return new(rows);
    }

    public static InlineKeyboardMarkup ShowAnswer(Guid sessionId, int cardIndex)
    {
        return new(InlineKeyboardButton.WithCallbackData("👁 Show answer", $"show:{sessionId:N}:{cardIndex}"));
    }

    public static InlineKeyboardMarkup Answers(Guid sessionId, int cardIndex)
    {
        return new([
            [
                InlineKeyboardButton.WithCallbackData("❌ Again", $"answer:{sessionId:N}:{cardIndex}:again"),
                InlineKeyboardButton.WithCallbackData("😐 Hard", $"answer:{sessionId:N}:{cardIndex}:hard")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✅ Good", $"answer:{sessionId:N}:{cardIndex}:good"),
                InlineKeyboardButton.WithCallbackData("😎 Easy", $"answer:{sessionId:N}:{cardIndex}:easy")
            ],
            [InlineKeyboardButton.WithCallbackData("❓ What do these mean?", "rating_help")]
        ]);
    }

    public static InlineKeyboardMarkup TodayStart
    {
        get
        {
            return new(InlineKeyboardButton.WithCallbackData("Start today's session", "session:today:mixed"));
        }
    }

    public static InlineKeyboardMarkup ReviewStart
    {
        get
        {
            return new(InlineKeyboardButton.WithCallbackData("Start review", "choose:review"));
        }
    }

    public static InlineKeyboardMarkup SessionComplete
    {
        get
        {
            return new([
                [InlineKeyboardButton.WithCallbackData("🧠 Learn again", "learn-sources:1")],
                [
                    InlineKeyboardButton.WithCallbackData("📚 Vocabulary", "list:all:1"),
                    InlineKeyboardButton.WithCallbackData("📁 Collections", "collections")
                ]
            ]);
        }
    }
}
