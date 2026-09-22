namespace LexiLoop.Services.Models;

public record VocabularyFilter(string Type, string Status)
{
    public const string All = "a";
    public const string Words = "w";
    public const string Phrases = "p";
    public const string New = "n";
    public const string InProgress = "i";
    public const string Learned = "l";

    public string Key => $"{Type}.{Status}";

    public static VocabularyFilter Parse(string value)
    {
        var parts = value.ToLowerInvariant().Split('.', 2);
        if (parts.Length == 2 && parts[0] is All or Words or Phrases &&
            parts[1] is All or New or InProgress or Learned)
            return new(parts[0], parts[1]);
        return value.ToLowerInvariant() switch
        {
            "words" => new(Words, All),
            "phrases" => new(Phrases, All),
            "new" => new(All, New),
            "learning" => new(All, InProgress),
            "learned" => new(All, Learned),
            _ => new(All, All)
        };
    }
}
