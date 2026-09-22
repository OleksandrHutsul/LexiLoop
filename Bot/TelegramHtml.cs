using System.Net;

namespace LexiLoop.Bot;

internal static class TelegramHtml
{
    public static string Encode(string text) => WebUtility.HtmlEncode(text);
}
