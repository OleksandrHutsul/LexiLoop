using System.Text;
using System.Text.RegularExpressions;
using LexiLoop.Models.Enums;
using LexiLoop.Services.Interfaces;
using LexiLoop.Services.Models;
using Microsoft.VisualBasic.FileIO;

namespace LexiLoop.Services;

public class CsvVocabularyParser : ICsvVocabularyParser
{
    private static readonly HashSet<string> ForeignHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "word", "foreign", "term", "german", "english", "wort", "vocabulary", "front", "source"
    };

    private static readonly HashSet<string> TranslationHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "translation", "meaning", "ukrainian", "bedeutung", "back", "target", "definition"
    };

    private static readonly HashSet<string> PronunciationHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "pronunciation", "transcription", "phonetic", "ipa", "aussprache"
    };

    private static readonly Regex SpacedDashRegex = new(@"\s+[\-–—]\s+", RegexOptions.Compiled);

    static CsvVocabularyParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public CsvDocument Parse(byte[] bytes)
    {
        if (bytes.Length == 0)
            return Failure("The uploaded file is empty.");

        var text = Decode(bytes);
        var candidates = new[]
        {
            ParseCandidate(text, ";", "semicolon"),
            ParseCandidate(text, ",", "comma"),
            ParseCandidate(text, "\t", "tab")
        };

        var best = candidates[0];

        for (var i = 1; i < candidates.Length; i++)
        {
            if (candidates[i].Score > best.Score)
                best = candidates[i];
        }

        IReadOnlyList<string[]> rows;
        var delimiterName = best.Name;

        if (best.Score <= 0)
        {
            rows = ParseDashRows(text);
            delimiterName = "dash";
        }
        else
        {
            rows = best.Rows;
        }

        if (rows.Count == 0)
            return Failure("No rows were found in the file.");

        var columnCount = 0;

        foreach (var row in rows)
            columnCount = Math.Max(columnCount, row.Length);

        if (columnCount < 2)
            return Failure("The file appears to contain only one column. Use comma, semicolon, tab, or spaced dash separators.");

        var header = rows[0];
        var foreign = FindHeader(header, ForeignHeaders);
        var translation = FindHeader(header, TranslationHeaders);
        var pronunciation = FindHeader(header, PronunciationHeaders);

        var recognizedHeaders = 0;

        if (foreign is not null)
            recognizedHeaders++;

        if (translation is not null)
            recognizedHeaders++;

        if (pronunciation is not null)
            recognizedHeaders++;

        if (foreign is not null && translation is not null && foreign != translation)
            return new(rows, delimiterName, true, false, new CsvColumnMapping(foreign.Value, translation.Value, pronunciation), null);

        if (recognizedHeaders > 0)
            return new(rows, delimiterName, true, false, null, "Could not determine both the word and translation columns. Please map them manually.");

        if (columnCount == 2)
            return new(rows, delimiterName, false, false, new CsvColumnMapping(0, 1, null), null);

        if (columnCount == 3)
            return new(rows, delimiterName, false, false, new CsvColumnMapping(0, 2, 1), null);

        return new(rows, delimiterName, false, true, null, "The file has multiple unknown columns. Please identify the header row and map the columns.");
    }

    public CsvMappedResult Map(CsvDocument document, CsvColumnMapping mapping, bool hasHeader)
    {
        var inputs = new List<VocabularyInput>();
        var errors = new List<string>();
        var seen = new HashSet<(string Foreign, string Translation)>();
        var duplicates = 0;
        var total = 0;
        var startIndex = hasHeader ? 1 : 0;

        for (var i = startIndex; i < document.Rows.Count; i++)
        {
            var row = document.Rows[i];

            if (IsEmptyRow(row))
                continue;

            total++;

            var foreign = Value(row, mapping.ForeignIndex);
            var translation = Value(row, mapping.TranslationIndex);
            var pronunciation = mapping.PronunciationIndex is int pronunciationIndex ? Value(row, pronunciationIndex) : null;

            if (string.IsNullOrWhiteSpace(foreign) || string.IsNullOrWhiteSpace(translation))
            {
                errors.Add($"Row {i + 1}: word and translation are required.");
                continue;
            }

            foreign = foreign.Trim();
            translation = translation.Trim();
            pronunciation = string.IsNullOrWhiteSpace(pronunciation) ? null : pronunciation.Trim();

            var key = (foreign.ToUpperInvariant(), translation.ToUpperInvariant());

            if (!seen.Add(key))
            {
                duplicates++;
                continue;
            }

            var type = foreign.Contains(' ') ? VocabularyItemType.Phrase : VocabularyItemType.Word;
            inputs.Add(new(foreign, translation, pronunciation, type));
        }

        return new(inputs, total, errors.Count, duplicates, errors);
    }

    private static CsvCandidate ParseCandidate(string text, string delimiter, string name)
    {
        try
        {
            using var reader = new StringReader(text);
            using var parser = new TextFieldParser(reader)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };

            parser.SetDelimiters(delimiter);

            var rows = new List<string[]>();

            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields() ?? [];

                if (!IsEmptyRow(fields))
                    rows.Add(fields);
            }

            if (rows.Count == 0)
                return new([], name, 0);

            var widths = new Dictionary<int, int>();

            foreach (var row in rows)
            {
                if (!widths.TryAdd(row.Length, 1))
                    widths[row.Length]++;
            }

            var modalWidth = 0;
            var modalCount = 0;

            foreach (var width in widths)
            {
                if (width.Value > modalCount || width.Value == modalCount && width.Key > modalWidth)
                {
                    modalWidth = width.Key;
                    modalCount = width.Value;
                }
            }

            if (modalWidth < 2)
                return new(rows, name, 0);

            var headerBonus = CountRecognizedHeaders(rows[0]) * 20;
            var score = modalCount * 10 + headerBonus - (rows.Count - modalCount) * 3;

            return new(rows, name, score);
        }
        catch (MalformedLineException)
        {
            return new([], name, 0);
        }
    }

    private static IReadOnlyList<string[]> ParseDashRows(string text)
    {
        var rows = new List<string[]>();
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var values = SpacedDashRegex.Split(line, 3);
            var fields = new string[values.Length];

            for (var i = 0; i < values.Length; i++)
                fields[i] = values[i].Trim();

            if (fields.Length >= 2)
                rows.Add(fields);
        }

        return rows;
    }

    private static int CountRecognizedHeaders(string[] row)
    {
        var count = 0;

        foreach (var value in row)
        {
            var header = NormalizeHeader(value);

            if (ForeignHeaders.Contains(header) || TranslationHeaders.Contains(header) || PronunciationHeaders.Contains(header))
                count++;
        }

        return count;
    }

    private static int? FindHeader(string[] row, HashSet<string> names)
    {
        for (var i = 0; i < row.Length; i++)
        {
            if (names.Contains(NormalizeHeader(row[i])))
                return i;
        }

        return null;
    }

    private static string NormalizeHeader(string value)
    {
        var builder = new StringBuilder();

        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static string? Value(string[] row, int index)
    {
        return index >= 0 && index < row.Length ? row[index] : null;
    }

    private static bool IsEmptyRow(string[] row)
    {
        foreach (var value in row)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return false;
        }

        return true;
    }

    private static CsvDocument Failure(string error)
    {
        return new([], "unknown", false, false, null, error);
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
            return Encoding.UTF8.GetString(bytes, Encoding.UTF8.Preamble.Length, bytes.Length - Encoding.UTF8.Preamble.Length);

        if (bytes.AsSpan().StartsWith(Encoding.Unicode.Preamble))
            return Encoding.Unicode.GetString(bytes, Encoding.Unicode.Preamble.Length, bytes.Length - Encoding.Unicode.Preamble.Length);

        if (bytes.AsSpan().StartsWith(Encoding.BigEndianUnicode.Preamble))
            return Encoding.BigEndianUnicode.GetString(bytes, Encoding.BigEndianUnicode.Preamble.Length, bytes.Length - Encoding.BigEndianUnicode.Preamble.Length);

        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            var windows1251 = Encoding.GetEncoding(1251).GetString(bytes);
            var windows1252 = Encoding.GetEncoding(1252).GetString(bytes);

            return LongestCyrillicRun(windows1251) >= 2 ? windows1251 : windows1252;
        }
    }

    private static int LongestCyrillicRun(string value)
    {
        var longest = 0;
        var current = 0;

        foreach (var character in value)
        {
            current = character is >= '\u0400' and <= '\u04FF' ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return longest;
    }
}
