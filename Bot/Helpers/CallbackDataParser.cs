namespace LexiLoop.Bot.Helpers;

public static class CallbackDataParser
{
    public static bool TryGetLong(string[] parts, int index, out long value)
    {
        value = default;
        return index < parts.Length && long.TryParse(parts[index], out value);
    }

    public static bool TryGetInt(string[] parts, int index, out int value)
    {
        value = default;
        return index < parts.Length && int.TryParse(parts[index], out value);
    }

    public static bool TryGetString(string[] parts, int index, out string value)
    {
        if (index < parts.Length && !string.IsNullOrWhiteSpace(parts[index]))
        {
            value = parts[index];
            return true;
        }

        value = string.Empty;
        return false;
    }
}