using LexiLoop.Bot.Helpers;
using LexiLoop.Services.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace LexiLoop.Bot.Keyboards;

public static class ImportKeyboard
{
    public static InlineKeyboardMarkup CsvHeaderChoice(string token)
    {
        return new([
            [
                InlineKeyboardButton.WithCallbackData("First row is headers", $"csv-header:{token}:yes"),
                InlineKeyboardButton.WithCallbackData("First row is data", $"csv-header:{token}:no")
            ],
            [InlineKeyboardButton.WithCallbackData("Cancel", $"csv-cancel:{token}")]
        ]);
    }

    public static InlineKeyboardMarkup CsvColumnChoice(string token, string field, CsvDocument document)
    {
        var sample = document.Rows.Count == 0 ? [] : document.Rows[0];
        var rows = new List<InlineKeyboardButton[]>();
        var currentRow = new List<InlineKeyboardButton>();

        for (var index = 0; index < document.ColumnCount; index++)
        {
            var value = index < sample.Length && sample[index].Length > 0 ? sample[index] : $"Column {index + 1}";
            var button = InlineKeyboardButton.WithCallbackData($"{index + 1}: {KeyboardText.TrimButtonText(value)}", $"csv-map:{token}:{field}:{index}");

            currentRow.Add(button);

            if (currentRow.Count == 2)
            {
                rows.Add(currentRow.ToArray());
                currentRow.Clear();
            }
        }

        if (currentRow.Count > 0)
            rows.Add(currentRow.ToArray());

        if (field == "pron")
            rows.Add([InlineKeyboardButton.WithCallbackData("No pronunciation column", $"csv-map:{token}:pron:skip")]);

        rows.Add([InlineKeyboardButton.WithCallbackData("Cancel", $"csv-cancel:{token}")]);

        return new(rows);
    }

    public static InlineKeyboardMarkup CsvConfirm(string token)
    {
        return new([
            [
                InlineKeyboardButton.WithCallbackData("✅ Import", $"csv-confirm:{token}"),
                InlineKeyboardButton.WithCallbackData("Cancel", $"csv-cancel:{token}")
            ]
        ]);
    }

    public static InlineKeyboardMarkup CsvCancel(string token)
    {
        return new(InlineKeyboardButton.WithCallbackData("Cancel", $"csv-cancel:{token}"));
    }

    public static InlineKeyboardMarkup ImportComplete
    {
        get
        {
            return new([
                [InlineKeyboardButton.WithCallbackData("🗂 Organize uncategorized", "list:uncategorized:a.a:1")],
                [InlineKeyboardButton.WithCallbackData("📚 Vocabulary", "list:all:a.a:1")]
            ]);
        }
    }
}
