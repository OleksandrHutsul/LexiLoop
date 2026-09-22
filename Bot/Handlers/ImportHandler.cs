using LexiLoop.Bot.Helpers;
using LexiLoop.Bot.Keyboards;
using LexiLoop.Bot.Models;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using LexiLoop.Services;
using LexiLoop.Services.Interfaces;
using LexiLoop.Services.Models;
using System.Collections.Concurrent;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace LexiLoop.Bot.Handlers;

public class ImportHandler
{
    private readonly UserService _userService;
    private readonly VocabularyService _vocabularyService;
    private readonly VocabularyImportService _vocabularyImportService;
    private readonly IVocabularyParser _vocabularyParser;
    private readonly ICsvVocabularyParser _csvVocabularyParser;

    private readonly ConcurrentDictionary<long, PendingCsvImportModel> _pendingCsvImports = new();

    public ImportHandler(UserService userService, VocabularyService vocabularyService, VocabularyImportService vocabularyImportService,
        IVocabularyParser vocabularyParser, ICsvVocabularyParser csvVocabularyParser)
    {
        _userService = userService;
        _vocabularyService = vocabularyService;
        _vocabularyImportService = vocabularyImportService;
        _vocabularyParser = vocabularyParser;
        _csvVocabularyParser = csvVocabularyParser;
    }

    public async Task StartAddAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        await _userService.SetInputModeAsync(userId, UserInputMode.Adding, cancellationToken);
        await telegramBotClient.SendMessage(chatId, AddInstructions, ParseMode.Html, cancellationToken: cancellationToken);
    }

    public async Task StartImportAsync(ITelegramBotClient telegramBotClient, long chatId, long userId, CancellationToken cancellationToken)
    {
        _pendingCsvImports.TryRemove(userId, out _);

        await _userService.SetInputModeAsync(userId, UserInputMode.Importing, cancellationToken);
        await telegramBotClient.SendMessage(chatId, ImportInstructions, ParseMode.Html, cancellationToken: cancellationToken);
    }

    public void ClearPending(long userId)
    {
        _pendingCsvImports.TryRemove(userId, out _);
    }

    public async Task HandleImportTextAsync(ITelegramBotClient telegramBotClient, long chatId, TelegramUser telegramUser, string text, CancellationToken cancellationToken)
    {
        var parsed = telegramUser.InputMode == UserInputMode.Importing
            ? _vocabularyParser.ParseJson(text)
            : _vocabularyParser.ParseLines(text);

        if (parsed.Valid.Count == 0)
        {
            var guidance = telegramUser.InputMode == UserInputMode.Importing
                ? "📥 I couldn't import that JSON. Send a JSON array like the example above, upload a CSV/TSV file, or use /cancel to stop."
                : "➕ I couldn't find a valid word / phrase. Use: <code>foreign text - translation - pronunciation</code>\nPronunciation is optional. Use /cancel to stop.";

            var details = parsed.Errors.Count == 0
                ? ""
                : $"\n\n{TelegramHtml.Encode(string.Join('\n', parsed.Errors.Take(3)))}";

            await telegramBotClient.SendMessage(chatId, guidance + details, ParseMode.Html, cancellationToken: cancellationToken);
            return;
        }

        var result = await _vocabularyService.AddAsync(telegramUser.Id, parsed.Valid, cancellationToken);
        await _userService.SetInputModeAsync(telegramUser.Id, UserInputMode.None, cancellationToken);

        var response = new StringBuilder($"✅ Added {result.Added} items\n\nWords: {result.Words}\nPhrases: {result.Phrases}");

        if (result.Duplicates > 0)
            response.Append($"\nDuplicates skipped: {result.Duplicates}");

        if (parsed.Errors.Count > 0)
        {
            response.Append($"\n\n⚠️ Invalid: {parsed.Errors.Count}\n");
            response.Append(string.Join('\n', parsed.Errors.Take(5)));
        }

        if (telegramUser.InputMode == UserInputMode.Importing)
            await telegramBotClient.SendMessage(chatId, response.ToString(), replyMarkup: ImportKeyboard.ImportComplete, cancellationToken: cancellationToken);
        else
            await telegramBotClient.SendMessage(chatId, response.ToString(), replyMarkup: MainMenuKeyboard.MainMenu, cancellationToken: cancellationToken);
    }

    public async Task HandleCsvDocumentAsync(ITelegramBotClient telegramBotClient, long chatId, TelegramUser telegramUser, Document document, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(document.FileName ?? "");

        if (!extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase))
        {
            await telegramBotClient.SendMessage(chatId, "Please upload a .csv or .tsv file. Use /cancel to stop importing.", cancellationToken: cancellationToken);
            return;
        }

        if (document.FileSize is > 20 * 1024 * 1024)
        {
            await telegramBotClient.SendMessage(chatId, "That file is too large for Telegram's bot download limit (20 MB).", cancellationToken: cancellationToken);
            return;
        }

        await using var stream = new MemoryStream();
        await telegramBotClient.GetInfoAndDownloadFile(document.FileId, stream, cancellationToken);

        var parsedDocument = _csvVocabularyParser.Parse(stream.ToArray());

        if (parsedDocument.Rows.Count == 0)
        {
            var error = TelegramHtml.Encode(parsedDocument.Error ?? "No CSV rows were found.");

            await telegramBotClient.SendMessage(chatId, $"I couldn't read that file. {error}", ParseMode.Html, cancellationToken: cancellationToken);
            return;
        }

        var token = Guid.NewGuid().ToString("N")[..8];
        var pending = new PendingCsvImportModel(token, parsedDocument);

        _pendingCsvImports[telegramUser.Id] = pending;

        if (parsedDocument.DetectedMapping is not null)
        {
            pending.HasHeader = parsedDocument.HasHeader;
            await PrepareCsvPreviewAsync(telegramBotClient, chatId, null, telegramUser.Id, pending, parsedDocument.DetectedMapping, cancellationToken);
            return;
        }

        if (parsedDocument.HeaderDecisionRequired)
        {
            await telegramBotClient.SendMessage(chatId,
                $"I found {parsedDocument.ColumnCount} columns using {parsedDocument.DelimiterName} separators. Does the first row contain column names?",
                replyMarkup: ImportKeyboard.CsvHeaderChoice(token), cancellationToken: cancellationToken);
            return;
        }

        pending.HasHeader = parsedDocument.HasHeader;

        await telegramBotClient.SendMessage(chatId, "Choose the column containing the word or phrase:",
            replyMarkup: ImportKeyboard.CsvColumnChoice(token, "word", parsedDocument), cancellationToken: cancellationToken);
    }

    public async Task HandleCallbackAsync(ITelegramBotClient telegramBotClient, CallbackQuery callbackQuery, TelegramUser telegramUser,
        string[] parts, CancellationToken cancellationToken)
    {
        if (parts.Length < 2 || !_pendingCsvImports.TryGetValue(telegramUser.Id, out var pending) || pending.Token != parts[1]) return;

        if (callbackQuery.Message is null) return;

        var chatId = callbackQuery.Message.Chat.Id;
        var messageId = callbackQuery.Message.Id;

        switch (parts[0])
        {
            case "csv-cancel":
                _pendingCsvImports.TryRemove(telegramUser.Id, out _);
                await _userService.SetInputModeAsync(telegramUser.Id, UserInputMode.None, cancellationToken);
                await telegramBotClient.EditMessageText(chatId, messageId, "CSV import cancelled.", cancellationToken: cancellationToken);
                break;

            case "csv-header" 
                when parts.Length == 3 && parts[2] is "yes" or "no":
                pending.HasHeader = parts[2] == "yes";
                await telegramBotClient.EditMessageText(chatId, messageId, "Choose the column containing the word or phrase:",
                    replyMarkup: ImportKeyboard.CsvColumnChoice(pending.Token, "word", pending.Document), cancellationToken: cancellationToken);
                break;

            case "csv-map" 
                when parts.Length == 4:
                await HandleCsvMappingCallbackAsync(telegramBotClient, chatId, messageId, telegramUser.Id, pending, parts[2], parts[3], cancellationToken);
                break;

            case "csv-confirm" 
                when parts.Length == 2 && pending.Preview is not null:
                await ConfirmCsvImportAsync(telegramBotClient, chatId, messageId, telegramUser.Id, pending, cancellationToken);
                break;
        }
    }

    private async Task ConfirmCsvImportAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId,
        PendingCsvImportModel pending, CancellationToken cancellationToken)
    {
        var result = await _vocabularyService.AddAsync(userId, pending.Preview!.NewItems, cancellationToken);

        _pendingCsvImports.TryRemove(userId, out _);
        await _userService.SetInputModeAsync(userId, UserInputMode.None, cancellationToken);

        var text = $"✅ Imported {result.Added} items\n\nWords: {result.Words}\nPhrases: {result.Phrases}" +
            (result.Duplicates > 0 ? $"\nDuplicates skipped: {result.Duplicates}" : "");

        await telegramBotClient.EditMessageText(chatId, messageId, text, replyMarkup: ImportKeyboard.ImportComplete, cancellationToken: cancellationToken);
    }

    private async Task PrepareCsvPreviewAsync(ITelegramBotClient telegramBotClient, long chatId, int? messageId, long userId, PendingCsvImportModel pending, 
        CsvColumnMapping mapping, CancellationToken cancellationToken)
    {
        var mapped = _csvVocabularyParser.Map(pending.Document, mapping, pending.HasHeader ?? false);
        var preview = await _vocabularyImportService.PrepareAsync(userId, mapped, cancellationToken);

        pending.Preview = preview;

        var text = FormatCsvPreview(pending.Document, preview);
        var keyboard = preview.NewItems.Count > 0
            ? ImportKeyboard.CsvConfirm(pending.Token)
            : ImportKeyboard.CsvCancel(pending.Token);

        if (messageId is null)
        {
            await telegramBotClient.SendMessage(chatId, text, ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
            return;
        }

        await telegramBotClient.EditMessageText(chatId, messageId.Value, text, ParseMode.Html, replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private async Task HandleCsvMappingCallbackAsync(ITelegramBotClient telegramBotClient, long chatId, int messageId, long userId, PendingCsvImportModel pending, 
        string field, string value, CancellationToken cancellationToken)
    {
        if (pending.HasHeader is null) return;

        int? index = null;

        if (value != "skip")
        {
            if (!int.TryParse(value, out var parsedIndex)) return;
            if (parsedIndex < 0 || parsedIndex >= pending.Document.ColumnCount) return;

            index = parsedIndex;
        }

        if (field == "word")
        {
            pending.ForeignIndex = index;

            await telegramBotClient.EditMessageText(chatId, messageId, "Choose the translation column:",
                replyMarkup: ImportKeyboard.CsvColumnChoice(pending.Token, "trans", pending.Document), cancellationToken: cancellationToken);
            return;
        }

        if (field == "trans" && pending.ForeignIndex is not null)
        {
            if (index == pending.ForeignIndex)
            {
                await telegramBotClient.EditMessageText(chatId, messageId, "The translation must use a different column. Choose the translation column:",
                    replyMarkup: ImportKeyboard.CsvColumnChoice(pending.Token, "trans", pending.Document), cancellationToken: cancellationToken);
                return;
            }

            pending.TranslationIndex = index;

            await telegramBotClient.EditMessageText(chatId, messageId, "Choose the pronunciation column, if present:",
                replyMarkup: ImportKeyboard.CsvColumnChoice(pending.Token, "pron", pending.Document), cancellationToken: cancellationToken);
            return;
        }

        if (field != "pron" || pending.ForeignIndex is null || pending.TranslationIndex is null) return;

        if (index == pending.ForeignIndex || index == pending.TranslationIndex)
        {
            await telegramBotClient.EditMessageText(chatId, messageId, "Pronunciation must use a different column. Choose another column or select no pronunciation:",
                replyMarkup: ImportKeyboard.CsvColumnChoice(pending.Token, "pron", pending.Document), cancellationToken: cancellationToken);
            return;
        }

        var mapping = new CsvColumnMapping(pending.ForeignIndex.Value, pending.TranslationIndex.Value, index);
        await PrepareCsvPreviewAsync(telegramBotClient, chatId, messageId, userId, pending, mapping, cancellationToken);
    }

    private static string FormatCsvPreview(CsvDocument document, CsvImportPreview preview)
    {
        var text = new StringBuilder("📥 <b>CSV import preview</b>");

        text.Append($"\n\nRows found: {preview.TotalRows}");
        text.Append($"\nValid unique rows: {preview.ValidRows}");
        text.Append($"\nReady to import: {preview.NewItems.Count}");
        text.Append($"\nAlready in vocabulary: {preview.ExistingRows}");
        text.Append($"\nDuplicates in file: {preview.FileDuplicateRows}");
        text.Append($"\nInvalid rows: {preview.InvalidRows}");
        text.Append($"\nSeparator: {TelegramHtml.Encode(document.DelimiterName)}");

        if (preview.SampleItems.Count > 0)
        {
            text.Append("\n\n<b>Sample</b>");

            foreach (var item in preview.SampleItems)
            {
                text.Append($"\n• {TelegramHtml.Encode(item.ForeignText)} → {TelegramHtml.Encode(item.Translation)}" +
                    (item.Pronunciation is null ? "" : $" ({TelegramHtml.Encode(item.Pronunciation)})"));
            }
        }

        if (preview.Errors.Count > 0)
        {
            text.Append("\n\n⚠️ ");
            text.Append(TelegramHtml.Encode(string.Join('\n', preview.Errors.Take(3))));
        }

        if (preview.NewItems.Count == 0)
            text.Append("\n\nThere are no new valid items to import.");

        return text.ToString();
    }

    private const string AddInstructions = """
        ➕ <b>Add words and phrases</b>

        Send one or more lines in this format:
        <code>foreign text - translation</code>
        <code>foreign text - translation - pronunciation</code>

        Example:
        <code>der Tag - день - деа таак
        Guten Morgen - Доброго ранку - ґуутен моаґен</code>

        Send your vocabulary when ready.
        Use /cancel to stop.
        """;

    private const string ImportInstructions = """
        📥 <b>Import vocabulary</b>

        Upload a <b>CSV or TSV file</b> with:
        • word, translation
        • word, pronunciation, translation

        Comma, semicolon, tab, and spaced-dash separators are supported. Excel UTF-8/Unicode files are supported too. You will see a preview before anything is saved.

        Or send a JSON array in this format:

        <pre><code>[
          {
            "word": "der Tag",
            "translation": "день",
            "pronunciation": "деа таак",
            "type": "word"
          },
          {
            "word": "Guten Morgen",
            "translation": "Доброго ранку",
            "pronunciation": "ґуутен моаґен",
            "type": "phrase"
          }
        ]</code></pre>

        <b>Required:</b> word, translation
        <b>Optional:</b> pronunciation, type
        <b>Types:</b> "word" or "phrase"

        If type is omitted, text containing spaces is treated as a phrase.

        Upload the file or send the JSON when ready.
        Use /cancel to stop.
        """;
}
