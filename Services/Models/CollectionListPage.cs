namespace LexiLoop.Services.Models;

public record CollectionListPage(IReadOnlyList<CollectionSummary> Items, int Page, int PageCount, int Total);
