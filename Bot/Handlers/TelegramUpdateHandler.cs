using LexiLoop.Models.Enums;
using LexiLoop.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace LexiLoop.Bot.Handlers;

public class TelegramUpdateHandler
{
    private readonly ILogger<TelegramUpdateHandler> _logger;
    private readonly CommandHandler _commandHandler;
    private readonly ImportHandler _importHandler;
    private readonly LearningHandler _learningHandler;
    private readonly VocabularyHandler _vocabularyHandler;
    private readonly CollectionHandler _collectionHandler;
    private readonly SettingsHandler _settingsHandler;
    private readonly UserService _userService;

    public TelegramUpdateHandler(ILogger<TelegramUpdateHandler> logger, CommandHandler commandHandler, ImportHandler importHandler, LearningHandler learningHandler,
        VocabularyHandler vocabularyHandler, CollectionHandler collectionHandler, SettingsHandler settingsHandler, UserService userService)
    {
        _logger = logger;
        _commandHandler = commandHandler;
        _importHandler = importHandler;
        _learningHandler = learningHandler;
        _vocabularyHandler = vocabularyHandler;
        _collectionHandler = collectionHandler;
        _settingsHandler = settingsHandler;
        _userService = userService;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient telegramBotClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Message is { From: not null } message)
            {
                await HandleMessageAsync(telegramBotClient, message, cancellationToken);
            }
            else if (update.CallbackQuery is { From: not null } callbackQuery)
            {
                await HandleCallbackAsync(telegramBotClient, callbackQuery, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ApiRequestException exception)
        {
            _logger.LogWarning(exception, "Telegram API error while processing update {UpdateId}", update.Id);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled error while processing update {UpdateId}", update.Id);

            var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;

            if (chatId is not null)
                await telegramBotClient.SendMessage(chatId, "Something went wrong. Please try again.", cancellationToken: cancellationToken);
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient _, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram polling error");
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(ITelegramBotClient telegramBotClient, Message message, CancellationToken cancellationToken)
    {
        if (message.Text is null && message.Document is null) return;
        if (message.From is null) return;

        var telegramUser = await _userService.UpsertAsync(message.From.Id, message.From.Username, message.From.FirstName, cancellationToken);

        if (message.Document is not null)
        {
            if (telegramUser.InputMode != UserInputMode.Importing)
            {
                await telegramBotClient.SendMessage(message.Chat.Id, "Use /import before uploading a CSV file.", cancellationToken: cancellationToken);
                return;
            }

            await _importHandler.HandleCsvDocumentAsync(telegramBotClient, message.Chat.Id, telegramUser, message.Document, cancellationToken);
            return;
        }

        var text = message.Text!.Trim();

        if (CommandHandler.TryGetCommand(text, out var command, out var fullText))
        {
            await _commandHandler.HandleAsync(telegramBotClient, message.Chat.Id, telegramUser, command, fullText, cancellationToken);
            return;
        }

        if (telegramUser.InputMode is UserInputMode.SettingsNewItemsPerDay or UserInputMode.SettingsReviewNotificationTime)
        {
            await _settingsHandler.HandleSettingsInputAsync(telegramBotClient, message.Chat.Id, telegramUser, text, cancellationToken);
            return;
        }

        if (telegramUser.InputMode is UserInputMode.CreatingCollection or UserInputMode.RenamingCollection)
        {
            await _collectionHandler.HandleCollectionInputAsync(telegramBotClient, message.Chat.Id, telegramUser, text, cancellationToken);
            return;
        }

        if (telegramUser.InputMode is UserInputMode.Adding or UserInputMode.Importing)
        {
            await _importHandler.HandleImportTextAsync(telegramBotClient, message.Chat.Id, telegramUser, text, cancellationToken);
            return;
        }

        await _learningHandler.HandleFreeTextAsync(telegramBotClient, message, telegramUser, text, cancellationToken);
    }

    private async Task HandleCallbackAsync(ITelegramBotClient telegramBotClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Data == "rating_help")
        {
            await _learningHandler.HandleRatingHelpAsync(telegramBotClient, callbackQuery, cancellationToken);
            return;
        }

        await telegramBotClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

        if (callbackQuery.Data is null || callbackQuery.Message is null) return;

        var telegramUser = await _userService.UpsertAsync(callbackQuery.From.Id, callbackQuery.From.Username, callbackQuery.From.FirstName, cancellationToken);

        var parts = callbackQuery.Data.Split(':');

        switch (parts[0])
        {
            case "noop":
                return;

            case "csv-header" or "csv-map" or "csv-confirm" or "csv-cancel":
                await _importHandler.HandleCallbackAsync(telegramBotClient, callbackQuery, telegramUser, parts, cancellationToken);
                break;

            case "choose" or "learn-source" or "learn-sources" or "learn-count" or "pool-source" or "pool-sources" or "pool-count" or "session" or "show"
                or "answer" or "collection-practice":
                await _learningHandler.HandleCallbackAsync(telegramBotClient, callbackQuery, telegramUser, parts, cancellationToken);
                break;

            case "list" or "filters" or "fs" or "ft" or "items" or "favorite" or "favorite-add" or "favorite-remove" or "vi" or "vfa" or "vfr" or "vic" or "vca" or "vcr":
                await _vocabularyHandler.HandleCallbackAsync(telegramBotClient, callbackQuery, telegramUser, parts, cancellationToken);
                break;

            case "collections" or "collection" or "collection-create" or "collection-rename" or "collection-delete" or "collection-delete-confirm" 
                or "collection-items" or "collection-add" or "collection-remove" or "collection-share" or "collection-share-revoke" or "share-view" 
                or "share-preview" or "share-import" or "uncat-collections" or "uncat-pick" or "uncat-create":
                await _collectionHandler.HandleCallbackAsync(telegramBotClient, callbackQuery, telegramUser, parts, cancellationToken);
                break;

            case "settings":
                await _settingsHandler.HandleCallbackAsync(telegramBotClient, callbackQuery, telegramUser, parts, cancellationToken);
                break;
        }
    }
}
