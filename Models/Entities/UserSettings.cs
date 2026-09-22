using LexiLoop.Models.Enums;

namespace LexiLoop.Models.Entities;

public class UserSettings
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public TelegramUser User { get; set; } = null!;
    public int NewItemsPerDay { get; set; } = 5;
    public LearningMode DefaultLearningMode { get; set; } = LearningMode.Mixed;
    public bool ShowPronunciation { get; set; } = true;
    public bool ReviewNotificationsEnabled { get; set; }
    public TimeOnly ReviewNotificationTime { get; set; } = new(19, 0);
    public DateOnly? LastNotificationDate { get; set; }
}
