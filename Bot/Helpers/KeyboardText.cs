namespace LexiLoop.Bot.Helpers;

public static class KeyboardText
{
    public static string TrimButtonText(string value, int maxLength = 48)
    {
        return value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
    }
}
