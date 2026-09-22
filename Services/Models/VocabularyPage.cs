using LexiLoop.Models.Entities;

namespace LexiLoop.Services.Models;

public record VocabularyPage(IReadOnlyList<VocabularyItem> Items, int Page, int PageCount, int Total, int Words, int Phrases)
{
    public string SourceKey { get; init; } = "all";
    public string SourceName { get; init; } = "Vocabulary";
    public string FilterKey { get; init; } = "a.a";
}
