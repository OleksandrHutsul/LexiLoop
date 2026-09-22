namespace LexiLoop.Models.Entities;

public class Collection
{
    public Collection()
    {
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public long Id { get; set; }
    public long UserId { get; set; }
    public TelegramUser User { get; set; } = null!;
    public string Name { get; set; } = "";
    public string? ShareToken { get; set; }
    public string? ImportedFromShareToken { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<VocabularyItemCollection> VocabularyItems { get; set; } = [];
}
