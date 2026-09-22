using LexiLoop.Models.Entities;

namespace LexiLoop.Services.Models;

public record CollectionPage(long Id, string Name, IReadOnlyList<VocabularyItem> Items, int Page, int PageCount, int Total);
