namespace LexiLoop.Services.Models;

public record ItemCollectionMembershipPage(long VocabularyItemId, IReadOnlyList<ItemCollectionMembership> Items, int Page, int PageCount, int Total);
