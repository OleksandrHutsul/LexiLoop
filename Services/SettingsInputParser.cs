using System.Globalization;

namespace LexiLoop.Services;

public static class SettingsInputParser
{
    private static readonly string[] TimeFormats = ["H:mm", "HH:mm"];

    public static bool TryParseNewItemsPerDay(string text, out int value)
    {
        return int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 1;
    }

    public static bool TryParseReviewTime(string text, out TimeOnly value)
    {
        return TimeOnly.TryParseExact(text.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }
}
