namespace LexiLoop.Services.Models;

public record UserStatistics(int Total, int Words, int Phrases, int New, int Learning, int Learned, int Due, int ReviewsToday, int CorrectPercent, 
    int CurrentStreak, int LongestStreak, int AddedThisWeek, int LearnedThisWeek, int ReviewsThisWeek);
