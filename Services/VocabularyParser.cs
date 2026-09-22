using System.Text.Json;
using LexiLoop.Models.Enums;
using LexiLoop.Services.Interfaces;
using LexiLoop.Services.Models;

namespace LexiLoop.Services;

public class VocabularyParser : IVocabularyParser
{
    public ParseResult ParseLines(string text)
    {
        var valid = new List<VocabularyInput>();
        var errors = new List<string>();
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < lines.Length; i++)
        {
            var parts = lines[i].Split(" - ", 3, StringSplitOptions.TrimEntries);

            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                errors.Add($"Line {i + 1}: expected foreign text - translation [- pronunciation]");
                continue;
            }

            var pronunciation = parts.Length == 3 ? NullIfWhiteSpace(parts[2]) : null;
            var type = parts[0].Contains(' ') ? VocabularyItemType.Phrase : VocabularyItemType.Word;

            valid.Add(new VocabularyInput(parts[0], parts[1], pronunciation, type));
        }

        return new ParseResult(valid, errors);
    }

    public ParseResult ParseJson(string json)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var rows = JsonSerializer.Deserialize<List<JsonVocabulary>>(json, options) ?? [];
            var valid = new List<VocabularyInput>();
            var errors = new List<string>();

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                if (string.IsNullOrWhiteSpace(row.Word) || string.IsNullOrWhiteSpace(row.Translation))
                {
                    errors.Add($"Item {i + 1}: word and translation are required");
                    continue;
                }

                var type = GetItemType(row);

                if (type is null)
                {
                    errors.Add($"Item {i + 1}: type must be word or phrase");
                    continue;
                }

                valid.Add(new VocabularyInput(row.Word.Trim(), row.Translation.Trim(), NullIfWhiteSpace(row.Pronunciation), type.Value));
            }

            return new ParseResult(valid, errors);
        }
        catch (JsonException ex)
        {
            return new ParseResult([], [$"Invalid JSON: {ex.Message}"]);
        }
    }

    private static VocabularyItemType? GetItemType(JsonVocabulary row)
    {
        if (row.Type?.Equals("phrase", StringComparison.OrdinalIgnoreCase) == true)
            return VocabularyItemType.Phrase;

        if (row.Type?.Equals("word", StringComparison.OrdinalIgnoreCase) == true)
            return VocabularyItemType.Word;

        if (row.Type is null)
            return row.Word.Contains(' ') ? VocabularyItemType.Phrase : VocabularyItemType.Word;

        return null;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
