using LexiLoop.Models.Enums;

namespace LexiLoop.Models.Entities;

public class LearningSession
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public TelegramUser User { get; set; } = null!;
    public long? CollectionId { get; set; }
    public string SourceName { get; set; } = "All words";
    public LearningSessionKind Kind { get; set; }
    public LearningMode Mode { get; set; }
    public LearningSessionStatus Status { get; set; }
    public long ChatId { get; set; }
    public int MessageId { get; set; }
    public int CurrentIndex { get; set; }
    public bool AnswerRevealed { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<LearningSessionCard> Cards { get; set; } = [];
}
