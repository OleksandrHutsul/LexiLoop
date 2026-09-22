namespace LexiLoop.Services.Models;

public record CollectionMembershipPage(long Id, string Name, IReadOnlyList<CollectionMembershipItem> Items, int Page, int PageCount, int Total);
