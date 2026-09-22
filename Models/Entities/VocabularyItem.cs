using LexiLoop.Models.Enums;

namespace LexiLoop.Models.Entities;

public class VocabularyItem
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public TelegramUser User { get; set; } = null!;
    public string ForeignText { get; set; } = "";
    public string Translation { get; set; } = "";
    public string? Pronunciation { get; set; }
    public VocabularyItemType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsLearningEnabled { get; set; }
    public bool IsFavorite { get; set; }
    public LearningProgress Progress { get; set; } = null!;
    public ICollection<VocabularyItemCollection> Collections { get; set; } = [];
}
