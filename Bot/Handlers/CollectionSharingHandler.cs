using LexiLoop.Bot.Helpers;
using LexiLoop.Bot.Keyboards;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using LexiLoop.Services;
using LexiLoop.Services.Models;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace LexiLoop.Bot.Handlers;

public class CollectionSharingHandler
{
    private readonly CollectionService _collectionService;

    public CollectionSharingHandler(CollectionService collectionService)
    {
        _collectionService = collectionService;
    }

    private const int PreviewPageSize = 10;

    public async Task HandleCallbackAsync(ITelegramBotClient telegramBotClient, CallbackQuery callback, TelegramUser user, string[] parts, CancellationToken cancellationToken)
    {
        if (callback.Message is null || parts.Length == 0) return;

        var chatId = callback.Message.Chat.Id;
        var messageId = callback.Message.Id;

        switch (parts[0])
        {
            case "collection-share"
                when CallbackDataParser.TryGetLong(parts, 1, out var collectionId):
                await SendCollectionShareAsync(telegramBotClient, chatId, messageId, user.Id, collectionId, cancellationToken);
                break;

            case "collection-share-revoke"
                when CallbackDataParser.TryGetLong(parts, 1, out var revokeCollectionId):
                await RevokeShareAsync(telegramBotClient, chatId, messageId, user.Id, revokeCollectionId, cancellationToken);
                break;

            case "share-view"
                when CallbackDataParser.TryGetString(parts, 1, out var token):
                await SendSharedCollectionAsync(telegramBotClient, chatId, messageId, user.Id, token, 1, false, cancellationToken);
                break;

            case "share-preview"
                when CallbackDataParser.TryGetString(parts, 1, out var previewToken) && CallbackDataParser.TryGetInt(parts, 2, out var page):
                await SendSharedCollectionAsync(telegramBotClient, chatId, messageId, user.Id, previewToken, page, true, cancellationToken);
                break;

            case "share-import"
                when CallbackDataParser.TryGetString(parts, 1, out var importToken):
                await ImportSharedCollectionAsync(telegramBotClient, chatId, messageId, user.Id, importToken, cancellationToken);
                break;
        }
    }

    public async Task SendSharedCollectionAsync(ITelegramBotClient telegramBotClient, long chatId, int? messageId, long viewerUserId, string token, int page, 
        bool preview, CancellationToken cancellationToken)
    {
        var shared = await _collectionService.GetSharedAsync(viewerUserId, token, page, PreviewPageSize, cancellationToken);

        if (shared is null)
        {
            await SendUnavailableAsync(telegramBotClient, chatId, messageId, cancellationToken);
            return;
        }

        var text = BuildSharedCollectionText(shared, preview);
        var keyboard = CollectionKeyboard.SharedCollection(shared, preview);

        if (messageId is null)
        {
            await telegramBotClient.SendMessage(chatId, text, ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
            return;
        }

        await telegramBotClient.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private async Task SendCollectionShareAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, long collectionId, CancellationToken cancellationToken)
    {
        var share = await _collectionService.GetOrCreateShareAsync(userId, collectionId, cancellationToken);

        if (share is null)
        {
            await telegramBotClient.EditMessageText(chatId, messageId, "⚠️ That list no longer exists.", cancellationToken: cancellationToken);
            return;
        }

        var me = await telegramBotClient.GetMe(cancellationToken);
        if (string.IsNullOrWhiteSpace(me.Username))
            throw new InvalidOperationException("The Telegram bot must have a username before share links can be created.");

        var url = $"https://t.me/{me.Username}?start=share_{share.Token}";
        var text = $"🔗 <b>Share {TelegramHtml.Encode(share.Name)}</b>\n\n {share.Total} words and phrases\n\n" +
            "Anyone with this link can preview the list and explicitly add a snapshot to their own lists:\n\n <code>{TelegramHtml.Encode(url)}</code>";

        await telegramBotClient.EditMessageText( chatId, messageId, text, ParseMode.Html, replyMarkup: CollectionKeyboard.CollectionShare(collectionId, url), cancellationToken: cancellationToken);
    }

    private async Task RevokeShareAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, long collectionId, CancellationToken cancellationToken)
    {
        var revoked = await _collectionService.RevokeShareAsync(userId, collectionId, cancellationToken);

        if (!revoked) return;

        await telegramBotClient.EditMessageText(chatId, messageId, "🚫 Share link disabled. Anyone using the old link will no longer be able to open this list.",
            replyMarkup: CollectionKeyboard.CollectionShareRevoked(collectionId), cancellationToken: cancellationToken);
    }

    private async Task ImportSharedCollectionAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, string token, CancellationToken cancellationToken)
    {
        var result = await _collectionService.ImportSharedAsync(userId, token, cancellationToken);

        switch (result.Status)
        {
            case SharedCollectionImportStatus.Imported:
                await telegramBotClient.EditMessageText(chatId, messageId,
                    $"✅ <b>{TelegramHtml.Encode(result.Name!)}</b> was added to your lists.\n\n New vocabulary: {result.AddedItems}\n Existing vocabulary reused: {result.ReusedItems}",
                    ParseMode.Html, replyMarkup: CollectionKeyboard.SharedCollectionImported(result.CollectionId!.Value), cancellationToken: cancellationToken);
                break;

            case SharedCollectionImportStatus.AlreadyImported:
                await telegramBotClient.EditMessageText(chatId, messageId, $"✅ <b>{TelegramHtml.Encode(result.Name!)}</b> is already in your lists.", ParseMode.Html,
                    replyMarkup: CollectionKeyboard.SharedCollectionImported(result.CollectionId!.Value), cancellationToken: cancellationToken);
                break;

            case SharedCollectionImportStatus.OwnCollection:
                await telegramBotClient.EditMessageText(chatId, messageId, "This shared link belongs to your own list.", replyMarkup: CollectionKeyboard.SharedCollectionUnavailable,
                    cancellationToken: cancellationToken);
                break;

            default:
                await SendUnavailableAsync(telegramBotClient, chatId, messageId, cancellationToken);
                break;
        }
    }

    private static string BuildSharedCollectionText(SharedCollectionPage shared, bool preview)
    {
        var text = new StringBuilder($"📚 <b>{TelegramHtml.Encode(shared.Name)}</b>\n Shared by {TelegramHtml.Encode(shared.OwnerName)}\n\n {shared.Total} words and phrases");

        if (shared.IsOwner)
            text.Append("\n\nThis is your list.");

        if (!preview)
            return text.ToString();

        text.Append("\n\n👀 <b>Preview</b>\n\n");

        if (shared.Items.Count == 0)
        {
            text.Append("This list is empty.");
            return text.ToString();
        }

        for (var i = 0; i < shared.Items.Count; i++)
        {
            var item = shared.Items[i];
            var number = (shared.Page - 1) * PreviewPageSize + i + 1;

            text.Append($"{number}. {TelegramHtml.Encode(item.ForeignText)} — {TelegramHtml.Encode(item.Translation)}\n");

            if (!string.IsNullOrWhiteSpace(item.Pronunciation))
                text.Append($"   {TelegramHtml.Encode(item.Pronunciation)}\n");

            text.Append('\n');
        }

        return text.ToString();
    }

    private static async Task SendUnavailableAsync(ITelegramBotClient telegramBotClient, long chatId, int? messageId, CancellationToken cancellationToken)
    {
        const string text = "🔒 This shared list is unavailable. The link may be invalid or disabled.";

        if (messageId is null)
        {
            await telegramBotClient.SendMessage(chatId, text, replyMarkup: CollectionKeyboard.SharedCollectionUnavailable, cancellationToken: cancellationToken);
            return;
        }

        await telegramBotClient.EditMessageText(chatId, messageId.Value, text, replyMarkup: CollectionKeyboard.SharedCollectionUnavailable, cancellationToken: cancellationToken);
    }
}
