using LexiLoop.Bot.Helpers;
using LexiLoop.Bot.Keyboards;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using LexiLoop.Services;
using LexiLoop.Services.Models;
using System.Collections.Concurrent;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace LexiLoop.Bot.Handlers;

public class CollectionHandler
{
    private readonly CollectionService _collectionService;
    private readonly UserService _userService;
    private readonly VocabularyHandler _vocabularyHandler;
    private readonly CollectionSharingHandler _sharingHandler;

    public CollectionHandler(CollectionService collectionService, UserService userService, VocabularyHandler vocabularyHandler, CollectionSharingHandler sharingHandler)
    {
        _collectionService = collectionService;
        _userService = userService;
        _vocabularyHandler = vocabularyHandler;
        _sharingHandler = sharingHandler;
    }

    private const int CollectionsPageSize = 8;
    private const int CollectionItemsPageSize = 10;
    private readonly ConcurrentDictionary<long, long> _pendingCollectionRenames = new();
    private readonly ConcurrentDictionary<long, int> _pendingUncategorizedCollections = new();

    public void ClearPending(long userId)
    {
        _pendingCollectionRenames.TryRemove(userId, out _);
        _pendingUncategorizedCollections.TryRemove(userId, out _);
    }

    public async Task HandleCallbackAsync(ITelegramBotClient telegramBotClient, CallbackQuery callbackQuery, TelegramUser telegramUser, string[] parts, CancellationToken cancellationToken)
    {
        if(callbackQuery.Message is null || parts.Length == 0) return;

        var chatId = callbackQuery.Message.Chat.Id;
        var messageId = callbackQuery.Message.Id;

        switch (parts[0])
        {
            case "collections":
                await HandleCollectionsCallbackAsync(telegramBotClient, chatId, messageId, telegramUser.Id, parts, cancellationToken);
                break;

            case "collection"
                when CallbackDataParser.TryGetLong(parts, 1, out var collectionId) && CallbackDataParser.TryGetInt(parts, 2, out var collectionPage):
                await SendCollectionAsync(telegramBotClient, chatId, messageId, telegramUser.Id, collectionId, collectionPage, cancellationToken);
                break;

            case "collection-create":
                await StartCollectionCreationAsync(telegramBotClient, chatId, telegramUser.Id, cancellationToken);
                break;

            case "collection-rename"
                when CallbackDataParser.TryGetLong(parts, 1, out var renameId):
                await StartCollectionRenameAsync(telegramBotClient, chatId, telegramUser.Id, renameId, cancellationToken);
                break;

            case "collection-delete"
                when CallbackDataParser.TryGetLong(parts, 1, out var deleteId):
                await RequestCollectionDeleteAsync(telegramBotClient, chatId, messageId, telegramUser.Id, deleteId, cancellationToken);
                break;

            case "collection-delete-confirm"
                when CallbackDataParser.TryGetLong(parts, 1, out var confirmDeleteId):
                await DeleteCollectionAsync(telegramBotClient, chatId, messageId, telegramUser.Id, confirmDeleteId, cancellationToken);
                break;

            case "collection-items"
                when CallbackDataParser.TryGetLong(parts, 1, out var editCollectionId) && CallbackDataParser.TryGetInt(parts, 2, out var membershipPage):
                await SendCollectionMembershipAsync(telegramBotClient, chatId, messageId, telegramUser.Id, editCollectionId, membershipPage, cancellationToken);
                break;

            case "collection-add":
            case "collection-remove":
                await HandleMembershipChangeAsync(telegramBotClient, chatId, messageId, telegramUser.Id, parts, cancellationToken);
                break;

            case "uncat-collections"
                when CallbackDataParser.TryGetInt(parts, 1, out var uncategorizedReturnPage) && CallbackDataParser.TryGetInt(parts, 2, out var uncategorizedCollectionsPage):
                await SendUncategorizedCollectionsAsync(telegramBotClient, chatId, messageId, telegramUser.Id, uncategorizedReturnPage, uncategorizedCollectionsPage, cancellationToken);
                break;

            case "uncat-pick"
                when CallbackDataParser.TryGetLong(parts, 1, out var uncategorizedCollectionId) && CallbackDataParser.TryGetInt(parts, 2, out var uncategorizedListPage):
                await AddUncategorizedToCollectionAsync(telegramBotClient, chatId, messageId, telegramUser.Id, uncategorizedCollectionId, uncategorizedListPage, cancellationToken);
                break;

            case "uncat-create"
                when CallbackDataParser.TryGetInt(parts, 1, out var createReturnPage):
                await StartUncategorizedCollectionCreationAsync(telegramBotClient, chatId, telegramUser.Id, createReturnPage, cancellationToken);
                break;

            case "collection-share":
            case "collection-share-revoke":
            case "share-view":
            case "share-preview":
            case "share-import":
                await _sharingHandler.HandleCallbackAsync(telegramBotClient, callbackQuery, telegramUser, parts, cancellationToken);
                break;
        }
    }

    public async Task SendCollectionsAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, string fullText, CancellationToken cancellationToken, 
        int? messageId = null, string prefix = "", int page = 1, string sourceKey = "all", string filter = "all", int returnPage = 1)
    {
        var command = fullText.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (command.Length == 2)
        {
            var result = await _collectionService.CreateWithResultAsync(userId, command[1], cancellationToken);
            prefix += GetCreateResultMessage(result);
            if (result.Status == CollectionCreateStatus.Created)
                page = int.MaxValue;
        }

        var collections = await _collectionService.GetListPageAsync(userId, page, CollectionsPageSize, cancellationToken);
        var prompt = collections.Total == 0
            ? "No collections yet. Create your first one below."
            : "Choose a collection:";
        var body = $"{prefix}📁 <b>Collections</b>\n\n{prompt}";
        var keyboard = CollectionKeyboard.Collections(collections, sourceKey, filter, returnPage);
        
        if (messageId is null)
        {
            await telegramBotClient.SendMessage(chatId, body, ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
            return;
        }

        await telegramBotClient.EditMessageText(chatId, messageId.Value, body, ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    public async Task HandleCollectionInputAsync(ITelegramBotClient telegramBotClient, long chatId, TelegramUser telegramUser, string text, CancellationToken cancellationToken)
    {
        if (telegramUser.InputMode == UserInputMode.CreatingCollection)
        {
            await HandleCreateInputAsync(telegramBotClient, chatId, telegramUser.Id, text, cancellationToken);
            return;
        }

        await HandleRenameInputAsync(telegramBotClient, chatId, telegramUser.Id, text, cancellationToken);
    }

    private async Task HandleCollectionsCallbackAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, string[] parts, CancellationToken cancellationToken)
    {
        var sourceKey = parts.Length == 5 ? parts[1] : "all";
        var filter = parts.Length == 5 ? parts[2] : "all";
        var returnPage = parts.Length == 5 && int.TryParse(parts[3], out var parsedReturnPage)
            ? parsedReturnPage
            : 1;
        
        var page = parts.Length switch
        {
            2 when int.TryParse(parts[1], out var parsedPage) => parsedPage,
            5 when int.TryParse(parts[4], out var parsedPage) => parsedPage,
            _ => 1
        };

        await SendCollectionsAsync(telegramBotClient, chatId, userId, "/collections", cancellationToken, messageId, page: page, sourceKey: sourceKey, filter: filter, 
            returnPage: returnPage);
    }

    private async Task StartCollectionCreationAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        ClearPending(userId);
        
        await _userService.SetInputModeAsync(userId, UserInputMode.CreatingCollection, cancellationToken);
        await telegramBotClient.SendMessage(chatId, "➕ Send a name for the new vocabulary list.\n\nUse /cancel to stop.", cancellationToken: cancellationToken);
    }

    private async Task StartCollectionRenameAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, long collectionId, CancellationToken cancellationToken)
    {
        var collection = await _collectionService.GetAsync(userId, collectionId, 1, 1, cancellationToken);
        if (collection is null) return;
        
        _pendingCollectionRenames[userId] = collectionId;
        
        await _userService.SetInputModeAsync(userId, UserInputMode.RenamingCollection, cancellationToken);
        await telegramBotClient.SendMessage(chatId, $"✏️ Send a new name for <b>{TelegramHtml.Encode(collection.Name)}</b>.\n\nUse /cancel to stop.", 
            ParseMode.Html, cancellationToken: cancellationToken);
    }

    private async Task RequestCollectionDeleteAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, long collectionId, CancellationToken cancellationToken)
    {
        var collection = await _collectionService.GetAsync(userId, collectionId, 1, 1, cancellationToken);
        if (collection is null) return;
        
        var text = $"Delete <b>{TelegramHtml.Encode(collection.Name)}</b>?\n\nIts vocabulary items will stay in your vocabulary and in any other lists.";
        var keyboard = CollectionKeyboard.ConfirmCollectionDelete(collectionId);
        
        await telegramBotClient.EditMessageText(chatId, messageId, text, ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private async Task DeleteCollectionAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, long collectionId, CancellationToken cancellationToken)
    {
        await _collectionService.DeleteAsync(userId, collectionId, cancellationToken);
        await SendCollectionsAsync(telegramBotClient, chatId, userId, "/collections", cancellationToken, messageId, "✅ List deleted.\n\n");
    }

    private async Task HandleMembershipChangeAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, string[] parts, CancellationToken cancellationToken)
    {
        if (!CallbackDataParser.TryGetLong(parts, 1, out var collectionId) || !CallbackDataParser.TryGetLong(parts, 2, out var itemId) 
            || !CallbackDataParser.TryGetInt(parts, 3, out var page)) return;

        if (parts[0] == "collection-add")
            await _collectionService.AddItemAsync(userId, collectionId, itemId, cancellationToken);
        else
            await _collectionService.RemoveItemAsync(userId, collectionId, itemId, cancellationToken);

        await SendCollectionMembershipAsync(telegramBotClient, chatId, messageId, userId, collectionId, page, cancellationToken);
    }

    private async Task AddUncategorizedToCollectionAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, long collectionId, int returnPage, CancellationToken cancellationToken)
    {
        var assigned = await _collectionService.AddUncategorizedAsync(userId, collectionId, cancellationToken);

        var prefix = assigned is null
            ? "⚠️ That collection no longer exists.\n\n"
            : $"✅ Added {assigned} items to the collection.\n\n";

        await _vocabularyHandler.SendListAsync(telegramBotClient, chatId, userId, "uncategorized", "a.a", returnPage, messageId, cancellationToken, prefix);
    }

    private async Task StartUncategorizedCollectionCreationAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, int returnPage, CancellationToken cancellationToken)
    {
        _pendingCollectionRenames.TryRemove(userId, out _);
        _pendingUncategorizedCollections[userId] = returnPage;

        await _userService.SetInputModeAsync(userId, UserInputMode.CreatingCollection, cancellationToken);

        await telegramBotClient.SendMessage(chatId, 
            "➕ Send a name for the new collection. All currently uncategorized items will be added to it.\n\nUse /cancel to stop.", cancellationToken: cancellationToken);
    }

    private async Task HandleCreateInputAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, string text, CancellationToken cancellationToken)
    {
        var result = await _collectionService.CreateWithResultAsync(userId, text, cancellationToken);

        if (result.Status != CollectionCreateStatus.Created)
        {
            var error = result.Status == CollectionCreateStatus.DuplicateName
                ? $"⚠️ Collection already exists: {TelegramHtml.Encode(result.Name)}\n\nSend a different name or use /cancel."
                : "⚠️ Please choose a name containing 1–100 characters, or use /cancel.";

            await telegramBotClient.SendMessage(chatId, error, ParseMode.Html, cancellationToken: cancellationToken);
            return;
        }

        await _userService.SetInputModeAsync(userId, UserInputMode.None, cancellationToken);

        if (_pendingUncategorizedCollections.TryRemove(userId, out var returnPage) && result.CollectionId is not null)
        {
            var assigned = await _collectionService.AddUncategorizedAsync(userId, result.CollectionId.Value, cancellationToken) ?? 0;

            await _vocabularyHandler.SendListAsync(telegramBotClient, chatId, userId, "uncategorized", "a.a", returnPage, null, cancellationToken,
                $"✅ Created {TelegramHtml.Encode(result.Name)} and added {assigned} items.\n\n");
            return;
        }

        await SendCollectionsAsync(telegramBotClient, chatId, userId, "/collections", cancellationToken,
            prefix: $"✅ Collection created: {TelegramHtml.Encode(result.Name)}\n\n", page: int.MaxValue);
    }

    private async Task HandleRenameInputAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, string text, CancellationToken cancellationToken)
    {
        if (!_pendingCollectionRenames.TryGetValue(userId, out var collectionId))
        {
            await _userService.SetInputModeAsync(userId, UserInputMode.None, cancellationToken);
            await telegramBotClient.SendMessage(chatId, "That rename request expired. Open the list and tap Rename again.", cancellationToken: cancellationToken);
            return;
        }

        var renamed = await _collectionService.RenameAsync(userId, collectionId, text, cancellationToken);

        if (!renamed)
        {
            await telegramBotClient.SendMessage(chatId, "⚠️ Please choose a non-empty, unique name of at most 100 characters, or use /cancel.", cancellationToken: cancellationToken);
            return;
        }

        _pendingCollectionRenames.TryRemove(userId, out _);

        await _userService.SetInputModeAsync(userId, UserInputMode.None, cancellationToken);
        await SendCollectionsAsync(telegramBotClient, chatId, userId, "/collections", cancellationToken, prefix: "✅ List renamed.\n\n");
    }

    private async Task SendUncategorizedCollectionsAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, int returnPage, 
        int collectionPage, CancellationToken cancellationToken)
    {
        var collections = await _collectionService.GetListPageAsync(userId, collectionPage, CollectionsPageSize, cancellationToken);

        var text = collections.Total == 0
            ? "📁 <b>Add all uncategorized items</b>\n\nYou do not have a collection yet. Create one to organize these items."
            : "📁 <b>Add all uncategorized items</b>\n\nChoose a collection. Every item that currently belongs to no collection will be added at once.";

        await telegramBotClient.EditMessageText(chatId, messageId, text, ParseMode.Html, replyMarkup: CollectionKeyboard.UncategorizedCollections(collections, returnPage), 
            cancellationToken: cancellationToken);
    }

    private async Task SendCollectionAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, long collectionId, int page, CancellationToken cancellationToken)
    {
        var collection = await _collectionService.GetAsync(userId, collectionId, page, CollectionItemsPageSize, cancellationToken);

        if (collection is null)
        {
            await SendCollectionsAsync(telegramBotClient, chatId, userId, "/collections", cancellationToken, messageId, "⚠️ That list no longer exists.\n\n");
            return;
        }

        var text = new StringBuilder($"📚 <b>{TelegramHtml.Encode(collection.Name)}</b>\n {collection.Total} items\n\n");

        if (collection.Items.Count == 0)
            text.Append("This list is empty.\n");

        for (var i = 0; i < collection.Items.Count; i++)
        {
            var item = collection.Items[i];
            var number = (collection.Page - 1) * CollectionItemsPageSize + i + 1;

            text.Append($"{number}. {TelegramHtml.Encode(item.ForeignText)} — {TelegramHtml.Encode(item.Translation)}\n");
        }

        await telegramBotClient.EditMessageText(chatId, messageId, text.ToString(), ParseMode.Html, 
            replyMarkup: CollectionKeyboard.Collection(collection.Id, collection.Page, collection.PageCount), cancellationToken: cancellationToken);
    }

    private async Task SendCollectionMembershipAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, long collectionId, int page, CancellationToken cancellationToken)
    {
        var membership = await _collectionService.GetMembershipPageAsync(userId, collectionId, page, CollectionItemsPageSize, cancellationToken);

        if (membership is null)
        {
            await SendCollectionsAsync(telegramBotClient, chatId, userId, "/collections", cancellationToken, messageId, "⚠️ That list no longer exists.\n\n");
            return;
        }

        var text = membership.Total == 0
            ? $"➕/➖ <b>Edit {TelegramHtml.Encode(membership.Name)}</b>\n\nYour vocabulary is empty. Add vocabulary first."
            : $"➕/➖ <b>Edit {TelegramHtml.Encode(membership.Name)}</b>\n\nTap an item to add or remove it. ✅ means it is in this list.";

        await telegramBotClient.EditMessageText(chatId, messageId, text, ParseMode.Html, replyMarkup: CollectionKeyboard.CollectionMembership(membership), cancellationToken: cancellationToken);
    }

    private static string GetCreateResultMessage(CollectionCreateResult result)
    {
        return result.Status switch
        {
            CollectionCreateStatus.Created => $"✅ Collection created: {TelegramHtml.Encode(result.Name)}\n\n",
            CollectionCreateStatus.DuplicateName =>  $"⚠️ Collection already exists: {TelegramHtml.Encode(result.Name)}\n\n",
            _ => "⚠️ Collection names must contain 1–100 characters.\n\n"
        };
    }
}
