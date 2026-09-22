using LexiLoop.Models.Enums;

namespace LexiLoop.Models.Entities;

public class TelegramUser
{
    public long Id { get; set; }
    public long TelegramUserId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public UserInputMode InputMode { get; set; }
    public UserSettings Settings { get; set; } = null!;
    public ICollection<VocabularyItem> VocabularyItems { get; set; } = [];
    public ICollection<Collection> Collections { get; set; } = [];
}
