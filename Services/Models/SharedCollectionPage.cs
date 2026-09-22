using LexiLoop.Models.Entities;

namespace LexiLoop.Services.Models;

public record SharedCollectionPage(string Token, string Name, string OwnerName, IReadOnlyList<VocabularyItem> Items, int Page, int PageCount, int Total, bool IsOwner);
