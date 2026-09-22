using LexiLoop.Bot.Helpers;
using LexiLoop.Services.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace LexiLoop.Bot.Keyboards;

public static class CollectionKeyboard
{
    public static InlineKeyboardMarkup Collections(CollectionListPage collections, string sourceKey = "all", string filter = "all", int returnPage = 1)
    {
        var rows = new List<InlineKeyboardButton[]>();

        foreach (var collection in collections.Items)
        {
            rows.Add([InlineKeyboardButton.WithCallbackData($"📚 {KeyboardText.TrimButtonText(collection.Name)} — {collection.Count}", 
                $"list:c{collection.Id}:{filter}:1")
            ]);
        }

        if (collections.PageCount > 1)
        {
            var nav = new List<InlineKeyboardButton>();

            if (collections.Page > 1)
                nav.Add(InlineKeyboardButton.WithCallbackData("◀ Previous", $"collections:{sourceKey}:{filter}:{returnPage}:{collections.Page - 1}"));

            nav.Add(InlineKeyboardButton.WithCallbackData($"{collections.Page} / {collections.PageCount}", "noop"));

            if (collections.Page < collections.PageCount)
                nav.Add(InlineKeyboardButton.WithCallbackData("More lists ▶", $"collections:{sourceKey}:{filter}:{returnPage}:{collections.Page + 1}"));

            rows.Add(nav.ToArray());
        }

        rows.Add([InlineKeyboardButton.WithCallbackData("➕ Create collection", "collection-create")]);
        rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Back", $"list:{sourceKey}:{filter}:{returnPage}")]);

        return new(rows);
    }

    public static InlineKeyboardMarkup Collection(long collectionId, int page, int pageCount)
    {
        var rows = new List<InlineKeyboardButton[]>();

        if (pageCount > 1)
        {
            var nav = new List<InlineKeyboardButton>();

            if (page > 1)
                nav.Add(InlineKeyboardButton.WithCallbackData("◀ Previous", $"collection:{collectionId}:{page - 1}"));

            nav.Add(InlineKeyboardButton.WithCallbackData($"{page} / {pageCount}", "noop"));

            if (page < pageCount)
                nav.Add(InlineKeyboardButton.WithCallbackData("Next ▶", $"collection:{collectionId}:{page + 1}"));

            rows.Add(nav.ToArray());
        }

        rows.Add([InlineKeyboardButton.WithCallbackData("🧠 Learn", $"learn-source:{collectionId}"),
            InlineKeyboardButton.WithCallbackData("🎲 Random practice", $"collection-practice:{collectionId}")
        ]);
        rows.Add([InlineKeyboardButton.WithCallbackData("➕/➖ Edit items", $"collection-items:{collectionId}:1")]);
        rows.Add([InlineKeyboardButton.WithCallbackData("🔗 Share list", $"collection-share:{collectionId}")]);
        rows.Add([InlineKeyboardButton.WithCallbackData("✏️ Rename", $"collection-rename:{collectionId}"),
            InlineKeyboardButton.WithCallbackData("🗑 Delete", $"collection-delete:{collectionId}")
        ]);
        rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Back", "collections:1")]);

        return new(rows);
    }

    public static InlineKeyboardMarkup UncategorizedCollections(CollectionListPage collections, int returnPage)
    {
        var rows = new List<InlineKeyboardButton[]>();

        foreach (var collection in collections.Items)
        {
            rows.Add([InlineKeyboardButton.WithCallbackData($"📁 {KeyboardText.TrimButtonText(collection.Name)}", $"uncat-pick:{collection.Id}:{returnPage}")]);
        }

        if (collections.PageCount > 1)
        {
            var nav = new List<InlineKeyboardButton>();

            if (collections.Page > 1)
                nav.Add(InlineKeyboardButton.WithCallbackData("◀", $"uncat-collections:{returnPage}:{collections.Page - 1}"));

            nav.Add(InlineKeyboardButton.WithCallbackData($"{collections.Page} / {collections.PageCount}", "noop"));

            if (collections.Page < collections.PageCount)
                nav.Add(InlineKeyboardButton.WithCallbackData("More ▶", $"uncat-collections:{returnPage}:{collections.Page + 1}"));

            rows.Add(nav.ToArray());
        }

        rows.Add([InlineKeyboardButton.WithCallbackData("➕ Create new collection", $"uncat-create:{returnPage}")]);
        rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Back", $"list:uncategorized:a.a:{returnPage}")]);

        return new(rows);
    }

    public static InlineKeyboardMarkup CollectionShare(long collectionId, string shareUrl)
    {
        return new([[InlineKeyboardButton.WithUrl("🔗 Open share link", shareUrl)],
            [InlineKeyboardButton.WithCallbackData("🚫 Disable share link", $"collection-share-revoke:{collectionId}")],
            [InlineKeyboardButton.WithCallbackData("⬅️ Back", $"collection:{collectionId}:1")]
        ]);
    }

    public static InlineKeyboardMarkup CollectionShareRevoked(long collectionId)
    {
        return new(InlineKeyboardButton.WithCallbackData("⬅️ Back to collection", $"collection:{collectionId}:1"));
    }

    public static InlineKeyboardMarkup SharedCollection(SharedCollectionPage page, bool preview)
    {
        var rows = new List<InlineKeyboardButton[]>();

        if (!preview)
        {
            rows.Add([InlineKeyboardButton.WithCallbackData("👀 Preview", $"share-preview:{page.Token}:1")]);
        }
        else if (page.PageCount > 1)
        {
            var nav = new List<InlineKeyboardButton>();

            if (page.Page > 1)
                nav.Add(InlineKeyboardButton.WithCallbackData("◀", $"share-preview:{page.Token}:{page.Page - 1}"));

            nav.Add(InlineKeyboardButton.WithCallbackData($"{page.Page} / {page.PageCount}", "noop"));

            if (page.Page < page.PageCount)
                nav.Add(InlineKeyboardButton.WithCallbackData("Next ▶", $"share-preview:{page.Token}:{page.Page + 1}"));

            rows.Add(nav.ToArray());
        }

        if (!page.IsOwner)
            rows.Add([InlineKeyboardButton.WithCallbackData("➕ Add to my lists", $"share-import:{page.Token}")]);

        if (preview)
            rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Back", $"share-view:{page.Token}")]);
        else
            rows.Add([InlineKeyboardButton.WithCallbackData("⬅️ Vocabulary", "list:all:a.a:1")]);

        return new(rows);
    }

    public static InlineKeyboardMarkup SharedCollectionImported(long collectionId)
    {
        return new([[InlineKeyboardButton.WithCallbackData("📁 Open imported list", $"list:c{collectionId}:a.a:1")],
            [InlineKeyboardButton.WithCallbackData("📚 Vocabulary", "list:all:a.a:1")]
        ]);
    }

    public static InlineKeyboardMarkup SharedCollectionUnavailable
    {
        get
        {
            return new(InlineKeyboardButton.WithCallbackData("📚 Vocabulary", "list:all:a.a:1"));
        }
    }

    public static InlineKeyboardMarkup CollectionMembership(CollectionMembershipPage page)
    {
        var rows = new List<InlineKeyboardButton[]>();

        foreach (var item in page.Items)
        {
            var icon = item.IsMember ? "✅" : "➕";
            var action = item.IsMember ? "remove" : "add";
            var text = KeyboardText.TrimButtonText($"{item.ForeignText} — {item.Translation}");

            rows.Add([InlineKeyboardButton.WithCallbackData($"{icon} {text}", $"collection-{action}:{page.Id}:{item.Id}:{page.Page}")]);
        }

        if (page.PageCount > 1)
        {
            var nav = new List<InlineKeyboardButton>();

            if (page.Page > 1)
                nav.Add(InlineKeyboardButton.WithCallbackData("◀ Previous", $"collection-items:{page.Id}:{page.Page - 1}"));

            nav.Add(InlineKeyboardButton.WithCallbackData($"{page.Page} / {page.PageCount}", "noop"));

            if (page.Page < page.PageCount)
                nav.Add(InlineKeyboardButton.WithCallbackData("Next ▶", $"collection-items:{page.Id}:{page.Page + 1}"));

            rows.Add(nav.ToArray());
        }

        rows.Add([InlineKeyboardButton.WithCallbackData("✅ Done", $"collection:{page.Id}:1")]);

        return new(rows);
    }

    public static InlineKeyboardMarkup ConfirmCollectionDelete(long collectionId)
    {
        return new([
            [
                InlineKeyboardButton.WithCallbackData("🗑 Yes, delete", $"collection-delete-confirm:{collectionId}"),
                InlineKeyboardButton.WithCallbackData("Cancel", $"collection:{collectionId}:1")
            ]
        ]);
    }
}
