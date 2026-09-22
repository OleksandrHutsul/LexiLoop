using LexiLoop.Bot.Helpers;
using LexiLoop.Models.Entities;
using LexiLoop.Services.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace LexiLoop.Bot.Keyboards;

public static class VocabularyKeyboard
{
    public static InlineKeyboardMarkup List(string sourceKey, string filter, int page, int pageCount, IReadOnlyList<VocabularyItem> items)
    {
        var state = VocabularyFilter.Parse(filter);
        filter = state.Key;

        var rows = new List<InlineKeyboardButton[]>();

        if (sourceKey == "all")
        {
            rows.Add([InlineKeyboardButton.WithCallbackData("📚 All", "list:all:all:1"),
                InlineKeyboardButton.WithCallbackData("📁 Collections", $"collections:{sourceKey}:{filter}:{page}:1"),
                InlineKeyboardButton.WithCallbackData("⭐ Favorites", "list:favorites:all:1")
            ]);
            rows.Add([InlineKeyboardButton.WithCallbackData("🗂 Uncategorized", "list:uncategorized:a.a:1")]);

            var wordsFilter = state.Type == VocabularyFilter.Words ? VocabularyFilter.All : VocabularyFilter.Words;
            var phrasesFilter = state.Type == VocabularyFilter.Phrases ? VocabularyFilter.All : VocabularyFilter.Phrases;

            rows.Add([InlineKeyboardButton.WithCallbackData(state.Type == VocabularyFilter.Words ? "✓ Words" : "Words", $"list:{sourceKey}:{wordsFilter}.{state.Status}:1"),
                InlineKeyboardButton.WithCallbackData(state.Type == VocabularyFilter.Phrases ? "✓ Phrases" : "Phrases", $"list:{sourceKey}:{phrasesFilter}.{state.Status}:1")
            ]);
        }
        else if (sourceKey == "uncategorized")
        {
            rows.Add([InlineKeyboardButton.WithCallbackData("📁 Add all to collection", $"uncat-collections:{page}:1")]);
        }
        else if (sourceKey == "favorites")
        {
            rows.Add([InlineKeyboardButton.WithCallbackData("🧠 Learn favorites", "learn-source:favorites")]);
        }
        else if (sourceKey.StartsWith('c') && long.TryParse(sourceKey[1..], out var collectionId))
        {
            rows.Add([InlineKeyboardButton.WithCallbackData("🧠 Learn", $"learn-source:{collectionId}")]);
            rows.Add([InlineKeyboardButton.WithCallbackData("✏️ Manage collection", $"collection:{collectionId}:1")]);
        }

        var actions = new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("⚙️ Filters", $"filters:{sourceKey}:{filter}:{page}")
        };

        if (items.Count > 0)
            actions.Add(InlineKeyboardButton.WithCallbackData("🔎 Open item", $"items:{sourceKey}:{filter}:{page}"));

        rows.Add(actions.ToArray());

        if (pageCount > 1)
        {
            var nav = new List<InlineKeyboardButton>();

            if (page > 1)
                nav.Add(InlineKeyboardButton.WithCallbackData("◀ Previous", $"list:{sourceKey}:{filter}:{page - 1}"));

            nav.Add(InlineKeyboardButton.WithCallbackData($"{page} / {pageCount}", "noop"));

            if (page < pageCount)
                nav.Add(InlineKeyboardButton.WithCallbackData("Next ▶", $"list:{sourceKey}:{filter}:{page + 1}"));

            rows.Add(nav.ToArray());
        }

        if (sourceKey == "favorites" || sourceKey == "uncategorized")
            rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Back", "list:all:a.a:1")]);
        else if (sourceKey.StartsWith('c'))
            rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Back", "collections:1")]);

        return new(rows);
    }

    public static InlineKeyboardMarkup VocabularyFilters(string sourceKey, string filter, int returnPage)
    {
        var state = VocabularyFilter.Parse(filter);

        return new([
            [
                InlineKeyboardButton.WithCallbackData(FilterLabel("All statuses", VocabularyFilter.All, state.Status), $"fs:{sourceKey}:{state.Type}:a:{returnPage}"),
                InlineKeyboardButton.WithCallbackData(FilterLabel("New", VocabularyFilter.New, state.Status), $"fs:{sourceKey}:{state.Type}:n:{returnPage}")
            ],
            [
                InlineKeyboardButton.WithCallbackData(FilterLabel("In progress", VocabularyFilter.InProgress, state.Status), $"fs:{sourceKey}:{state.Type}:i:{returnPage}"),
                InlineKeyboardButton.WithCallbackData(FilterLabel("Learned", VocabularyFilter.Learned, state.Status), $"fs:{sourceKey}:{state.Type}:l:{returnPage}")
            ],
            [
                InlineKeyboardButton.WithCallbackData(FilterLabel("All types", VocabularyFilter.All, state.Type), $"ft:{sourceKey}:a:{state.Status}:{returnPage}"),
                InlineKeyboardButton.WithCallbackData(FilterLabel("Words", VocabularyFilter.Words, state.Type), $"ft:{sourceKey}:w:{state.Status}:{returnPage}"),
                InlineKeyboardButton.WithCallbackData(FilterLabel("Phrases", VocabularyFilter.Phrases, state.Type), $"ft:{sourceKey}:p:{state.Status}:{returnPage}")
            ],
            [
                InlineKeyboardButton.WithCallbackData("✅ Apply", $"list:{sourceKey}:{state.Key}:1"),
                InlineKeyboardButton.WithCallbackData("🧹 Clear", $"filters:{sourceKey}:a.a:{returnPage}")
            ],
            [InlineKeyboardButton.WithCallbackData("⬅️ Back", $"list:{sourceKey}:{state.Key}:{returnPage}")]
        ]);
    }

    public static InlineKeyboardMarkup VocabularyItems(string sourceKey, string filter, int page, IReadOnlyList<VocabularyItem> items)
    {
        const int pageSize = 10;
        var rows = new List<InlineKeyboardButton[]>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var number = (page - 1) * pageSize + i + 1;

            rows.Add([InlineKeyboardButton.WithCallbackData($"{number}. {KeyboardText.TrimButtonText(item.ForeignText, 38)}", $"vi:{item.Id}:{sourceKey}:{filter}:{page}")]);
        }

        rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Back", $"list:{sourceKey}:{filter}:{page}")]);

        return new(rows);
    }

    public static InlineKeyboardMarkup VocabularyItem(VocabularyItem item, string sourceKey, string filter, int page)
    {
        var favoriteText = item.IsFavorite ? "⭐ Favorited" : "☆ Favorite";
        var favoriteAction = item.IsFavorite ? "vfr" : "vfa";

        return new([
            [
                InlineKeyboardButton.WithCallbackData(favoriteText, $"{favoriteAction}:{item.Id}:{sourceKey}:{filter}:{page}"),
                InlineKeyboardButton.WithCallbackData("📁 Collections", $"vic:{item.Id}:{sourceKey}:{filter}:{page}:1")
            ],
            [InlineKeyboardButton.WithCallbackData("⬅️ Back", $"items:{sourceKey}:{filter}:{page}")]
        ]);
    }

    public static InlineKeyboardMarkup ItemCollections(ItemCollectionMembershipPage memberships, string sourceKey, string filter, int returnPage)
    {
        var rows = new List<InlineKeyboardButton[]>();

        foreach (var collection in memberships.Items)
        {
            var icon = collection.IsMember ? "✅" : "➕";
            var action = collection.IsMember ? "vcr" : "vca";

            rows.Add([InlineKeyboardButton.WithCallbackData($"{icon} {KeyboardText.TrimButtonText(collection.Name)}",
                    $"{action}:{memberships.VocabularyItemId}:{collection.Id}:{sourceKey}:{filter}:{returnPage}:{memberships.Page}")
            ]);
        }

        if (memberships.PageCount > 1)
        {
            var nav = new List<InlineKeyboardButton>();

            if (memberships.Page > 1)
                nav.Add(InlineKeyboardButton.WithCallbackData("◀", $"vic:{memberships.VocabularyItemId}:{sourceKey}:{filter}:{returnPage}:{memberships.Page - 1}"));

            nav.Add(InlineKeyboardButton.WithCallbackData($"{memberships.Page} / {memberships.PageCount}", "noop"));

            if (memberships.Page < memberships.PageCount)
                nav.Add(InlineKeyboardButton.WithCallbackData("More ▶", $"vic:{memberships.VocabularyItemId}:{sourceKey}:{filter}:{returnPage}:{memberships.Page + 1}"));

            rows.Add(nav.ToArray());
        }

        rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Back", $"vi:{memberships.VocabularyItemId}:{sourceKey}:{filter}:{returnPage}")]);

        return new(rows);
    }

    private static string FilterLabel(string text, string value, string selected)
    {
        return value == selected ? $"✓ {text}" : text;
    }
}
