using System.Text;
using LexiLoop.Bot.Helpers;
using LexiLoop.Bot.Keyboards;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using LexiLoop.Services;
using LexiLoop.Services.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace LexiLoop.Bot.Handlers;

public class VocabularyHandler
{
    private readonly VocabularyService _vocabularyService;
    private readonly CollectionService _collectionService;

    public VocabularyHandler(VocabularyService vocabularyService, CollectionService collectionService)
    {
        _vocabularyService = vocabularyService;
        _collectionService = collectionService;
    }

    private const int VocabularyPageSize = 10;
    private const int CollectionsPageSize = 8;

    public async Task HandleCallbackAsync(ITelegramBotClient telegramBotClient, CallbackQuery callbackQuery, TelegramUser telegramUser, string[] parts, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null || parts.Length == 0) return;

        var chatId = callbackQuery.Message.Chat.Id;
        var messageId = callbackQuery.Message.Id;

        switch (parts[0])
        {
            case "list" 
                when parts.Length == 3 && CallbackDataParser.TryGetInt(parts, 2, out var page):
                await SendListAsync(telegramBotClient, chatId, telegramUser.Id, parts[1], page, messageId, cancellationToken);
                break;

            case "list" 
                when parts.Length == 4 && CallbackDataParser.TryGetInt(parts, 3, out var browsePage):
                await SendListAsync(telegramBotClient, chatId, telegramUser.Id, parts[1], parts[2], browsePage, messageId, cancellationToken);
                break;

            case "filters" 
                when parts.Length == 4 && CallbackDataParser.TryGetInt(parts, 3, out var filterReturnPage):
                await SendVocabularyFiltersAsync(telegramBotClient, chatId, messageId, parts[1], parts[2], filterReturnPage, cancellationToken);
                break;

            case "fs" or "ft" 
                when parts.Length == 5 && CallbackDataParser.TryGetInt(parts, 4, out var changedFilterReturnPage):
                await SendVocabularyFiltersAsync(telegramBotClient, chatId, messageId, parts[1],
                    $"{parts[2]}.{parts[3]}", changedFilterReturnPage, cancellationToken);
                break;

            case "items" 
                when parts.Length == 4 && CallbackDataParser.TryGetInt(parts, 3, out var itemsPage):
                await SendVocabularyItemsAsync(telegramBotClient, chatId, messageId, telegramUser.Id, parts[1], parts[2], itemsPage, cancellationToken);
                break;

            case "favorite" 
                when parts.Length == 4 &&  CallbackDataParser.TryGetLong(parts, 1, out var favoriteItemId) && 
                CallbackDataParser.TryGetInt(parts, 3, out var favoritePage):
                await _vocabularyService.ToggleFavoriteAsync(telegramUser.Id, favoriteItemId, cancellationToken);
                await SendListAsync(telegramBotClient, chatId, telegramUser.Id, parts[2], favoritePage, messageId, cancellationToken);
                break;

            case "favorite-add" or "favorite-remove" 
                when parts.Length == 4 && CallbackDataParser.TryGetLong(parts, 1, out var setFavoriteItemId) &&
                CallbackDataParser.TryGetInt(parts, 3, out var setFavoritePage):
                await _vocabularyService.SetFavoriteAsync(telegramUser.Id, setFavoriteItemId, parts[0] == "favorite-add", cancellationToken);
                await SendListAsync(telegramBotClient, chatId, telegramUser.Id, parts[2], setFavoritePage, messageId, cancellationToken);
                break;

            case "vi" 
            when parts.Length == 5 && CallbackDataParser.TryGetLong(parts, 1, out var vocabularyItemId) && CallbackDataParser.TryGetInt(parts, 4, out var itemReturnPage):
                await SendVocabularyItemAsync(telegramBotClient, chatId, messageId, telegramUser.Id, vocabularyItemId, parts[2], parts[3], itemReturnPage, cancellationToken);
                break;

            case "vfa" or "vfr" 
                when parts.Length == 5 && CallbackDataParser.TryGetLong(parts, 1, out var itemFavoriteId) && CallbackDataParser.TryGetInt(parts, 4, out var favoriteReturnPage):
                await _vocabularyService.SetFavoriteAsync(telegramUser.Id, itemFavoriteId, parts[0] == "vfa", cancellationToken);
                await SendVocabularyItemAsync(telegramBotClient, chatId, messageId, telegramUser.Id, itemFavoriteId, parts[2], parts[3], favoriteReturnPage, cancellationToken);
                break;

            case "vic" 
                when parts.Length == 6 && CallbackDataParser.TryGetLong(parts, 1, out var itemCollectionsId) &&
                CallbackDataParser.TryGetInt(parts, 4, out var itemCollectionListReturnPage) && CallbackDataParser.TryGetInt(parts, 5, out var itemCollectionsPage):
                await SendItemCollectionsAsync(telegramBotClient, chatId, messageId, telegramUser.Id, itemCollectionsId, parts[2], parts[3], 
                    itemCollectionListReturnPage, itemCollectionsPage, cancellationToken);
                break;

            case "vca" or "vcr" 
                when parts.Length == 7 && CallbackDataParser.TryGetLong(parts, 1, out var itemCollectionItemId) &&
                CallbackDataParser.TryGetLong(parts, 2, out var itemCollectionId) && CallbackDataParser.TryGetInt(parts, 5, out var itemCollectionReturnPage) &&
                CallbackDataParser.TryGetInt(parts, 6, out var itemCollectionPage):
                await UpdateCollectionMembershipAsync(telegramBotClient, chatId, messageId, telegramUser.Id,  itemCollectionItemId, itemCollectionId, 
                    parts[0] == "vca", parts[3], parts[4], itemCollectionReturnPage, itemCollectionPage, cancellationToken);
                break;
        }
    }

    public async Task SendListAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, string filter, int page, int? messageId, CancellationToken cancellationToken)
    {
        var sourceKey = filter.Equals("favorites", StringComparison.OrdinalIgnoreCase) ? "favorites" : "all";

        await SendListAsync(telegramBotClient, chatId, userId, sourceKey, VocabularyFilter.Parse(filter).Key, page, messageId, cancellationToken);
    }

    public async Task SendListAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, string sourceKey, string filter, int page, int? messageId, 
        CancellationToken cancellationToken, string prefix = "")
    {
        var data = await _vocabularyService.GetPageAsync(userId, sourceKey, filter, page, VocabularyPageSize, cancellationToken);
        if (data is null)
        {
            await SendListAsync(telegramBotClient, chatId, userId, "all", "all", 1, messageId, cancellationToken);
            return;
        }

        var icon = GetSourceIcon(data.SourceKey);
        var text = new StringBuilder(prefix).Append($"{icon} <b>{TelegramHtml.Encode(data.SourceName)}</b>\n\n");
        var activeFilter = VocabularyFilter.Parse(data.FilterKey);

        if (activeFilter.Type != VocabularyFilter.All || activeFilter.Status != VocabularyFilter.All)
            text.Append($"Filter: {GetTypeLabel(activeFilter.Type)} · {GetStatusLabel(activeFilter.Status)}\n\n");

        if (data.Items.Count == 0)
            text.Append("No items in this view.\n");

        for (var i = 0; i < data.Items.Count; i++)
        {
            var item = data.Items[i];
            var number = (data.Page - 1) * VocabularyPageSize + i + 1;

            text.Append($"{number}. {TelegramHtml.Encode(item.ForeignText)} — {TelegramHtml.Encode(item.Translation)}\n");

            if (!string.IsNullOrWhiteSpace(item.Pronunciation))
                text.Append($"   {TelegramHtml.Encode(item.Pronunciation)}\n");

            text.Append('\n');
        }

        text.Append(data.SourceKey == "all"
            ? $"Total: {data.Total}\nWords: {data.Words}\nPhrases: {data.Phrases}"
            : $"Total: {data.Total}");

        var keyboard = VocabularyKeyboard.List(data.SourceKey, data.FilterKey, data.Page, data.PageCount, data.Items);

        if (messageId is null)
        {
            await telegramBotClient.SendMessage(chatId, text.ToString(), ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
            return;
        }

        await telegramBotClient.EditMessageText(chatId, messageId.Value, text.ToString(), ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    internal static string FormatPronunciation(VocabularyItem item)
    {
        return !string.IsNullOrWhiteSpace(item.Pronunciation)
            ? $"\n🔊 Pronunciation: <b>{TelegramHtml.Encode(item.Pronunciation.Trim())}</b>"
            : "";
    }

    private static Task SendVocabularyFiltersAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, string sourceKey, string filter, int returnPage, 
        CancellationToken cancellationToken)
    {
        var state = VocabularyFilter.Parse(filter);
        var text = $"⚙️ <b>Vocabulary filters</b>\n\nLearning status: {GetStatusLabel(state.Status)}\nType: {GetTypeLabel(state.Type)}";

        return telegramBotClient.EditMessageText(chatId, messageId, text, ParseMode.Html, 
            replyMarkup: VocabularyKeyboard.VocabularyFilters(sourceKey, state.Key, returnPage), cancellationToken: cancellationToken);
    }

    private async Task SendVocabularyItemsAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, string sourceKey, string filter, 
        int page, CancellationToken cancellationToken)
    {
        var data = await _vocabularyService.GetPageAsync(userId, sourceKey, filter, page, VocabularyPageSize, cancellationToken);
        if (data is null)
        {
            await SendListAsync(telegramBotClient, chatId, userId, "all", "a.a", 1, messageId, cancellationToken);
            return;
        }

        await telegramBotClient.EditMessageText(chatId, messageId, $"🔎 <b>Open an item</b>\n\nChoose an item from page {data.Page}.", ParseMode.Html,
            replyMarkup: VocabularyKeyboard.VocabularyItems(data.SourceKey, data.FilterKey, data.Page, data.Items), cancellationToken: cancellationToken);
    }

    private async Task SendVocabularyItemAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, long vocabularyItemId, 
        string sourceKey, string filter, int page, CancellationToken cancellationToken)
    {
        var item = await _vocabularyService.GetItemAsync(userId, vocabularyItemId, cancellationToken);
        if (item is null)
        {
            await SendListAsync(telegramBotClient, chatId, userId, sourceKey, filter, page, messageId, cancellationToken);
            return;
        }

        var type = item.Type == VocabularyItemType.Word ? "Word" : "Phrase";
        var text = $"📚 <b>{TelegramHtml.Encode(item.ForeignText)}</b>\n\n💬 {TelegramHtml.Encode(item.Translation)}{FormatPronunciation(item)}\n\n{type}";

        await telegramBotClient.EditMessageText(chatId, messageId, text, ParseMode.Html, 
            replyMarkup: VocabularyKeyboard.VocabularyItem(item, sourceKey, filter, page), cancellationToken: cancellationToken);
    }

    private async Task SendItemCollectionsAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, long vocabularyItemId, 
        string sourceKey, string filter, int returnPage, int collectionPage, CancellationToken cancellationToken)
    {
        var item = await _vocabularyService.GetItemAsync(userId, vocabularyItemId, cancellationToken);
        var memberships = await _collectionService.GetItemMembershipPageAsync(userId, vocabularyItemId, collectionPage, CollectionsPageSize, cancellationToken);
        if (item is null || memberships is null)
        {
            await SendListAsync(telegramBotClient, chatId, userId, sourceKey, filter, returnPage, messageId, cancellationToken);
            return;
        }

        var prompt = memberships.Total == 0
            ? "You do not have any collections yet. Create one from the Collections screen."
            : "Tap a collection to add or remove this item. ✅ means it already belongs.";

        var text = $"📁 <b>Add “{TelegramHtml.Encode(item.ForeignText)}” to a collection</b>\n\n{prompt}";

        await telegramBotClient.EditMessageText(chatId, messageId, text, ParseMode.Html, 
            replyMarkup: VocabularyKeyboard.ItemCollections(memberships, sourceKey, filter, returnPage), cancellationToken: cancellationToken);
    }

    private async Task UpdateCollectionMembershipAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, long vocabularyItemId, 
        long collectionId, bool add, string sourceKey, string filter, int returnPage, int collectionPage, CancellationToken cancellationToken)
    {
        if (add)
            await _collectionService.AddItemAsync(userId, collectionId, vocabularyItemId, cancellationToken);
        else
            await _collectionService.RemoveItemAsync(userId, collectionId, vocabularyItemId, cancellationToken);

        await SendItemCollectionsAsync(telegramBotClient, chatId, messageId, userId, vocabularyItemId, sourceKey, filter, returnPage, collectionPage, cancellationToken);
    }

    private static string GetSourceIcon(string sourceKey)
    {
        if (sourceKey == "favorites") return "⭐";
        if (sourceKey == "uncategorized") return "🗂";
        if (sourceKey.StartsWith('c')) return "📁";

        return "📚";
    }

    private static string GetTypeLabel(string type)
    {
        return type switch
        {
            VocabularyFilter.Words => "Words",
            VocabularyFilter.Phrases => "Phrases",
            _ => "All types"
        };
    }

    private static string GetStatusLabel(string status)
    {
        return status switch
        {
            VocabularyFilter.New => "New",
            VocabularyFilter.InProgress => "In progress",
            VocabularyFilter.Learned => "Learned",
            _ => "All statuses"
        };
    }
}
