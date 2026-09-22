using LexiLoop.Data;
using LexiLoop.Models.Enums;
using LexiLoop.Services.Interfaces;
using LexiLoop.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace LexiLoop.Services;

public class StatisticsService
{
    private readonly IDbContextFactory<LexiLoopDbContext> _dbFactory;
    private readonly IAppClock _clock;

    public StatisticsService(IDbContextFactory<LexiLoopDbContext> dbFactory, IAppClock clock)
    {
        _dbFactory = dbFactory;
        _clock = clock;
    }

    public async Task<UserStatistics> GetAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var now = _clock.UtcNow;
        var today = _clock.LocalToday;
        var todayStartUtc = _clock.GetUtcStartOfDay(today);
        var tomorrowStartUtc = _clock.GetUtcStartOfDay(today.AddDays(1));
        var weekStartUtc = _clock.GetUtcStartOfDay(today.AddDays(-6));

        var items = db.VocabularyItems.Where(x => x.UserId == userId);
        var history = db.ReviewHistory.Where(x => x.VocabularyItem.UserId == userId);

        var total = await items.CountAsync(cancellationToken);
        var words = await items.CountAsync(x => x.Type == VocabularyItemType.Word, cancellationToken);
        var fresh = await items.CountAsync(x => x.Progress.ForeignToTranslationReviewCount == 0 && x.Progress.TranslationToForeignReviewCount == 0, cancellationToken);
        var learned = await items.CountAsync(x => x.Progress.ForeignToTranslationState == SrsCardState.Review && x.Progress.TranslationToForeignState == SrsCardState.Review, cancellationToken);
        var due = await items.CountAsync(x => x.IsLearningEnabled 
            && ((x.Progress.ForeignToTranslationReviewCount > 0 && x.Progress.ForeignToTranslationNextReviewAt <= now) 
            || (x.Progress.TranslationToForeignReviewCount > 0 && x.Progress.TranslationToForeignNextReviewAt <= now)), cancellationToken);

        var reviewsToday = await history.CountAsync(x => x.ReviewedAt >= todayStartUtc && x.ReviewedAt < tomorrowStartUtc, cancellationToken);
        var correctToday = await history.CountAsync(x => x.ReviewedAt >= todayStartUtc && x.ReviewedAt < tomorrowStartUtc && x.Result != ReviewResult.Again, cancellationToken);

        var reviewInstants = await history.Select(x => x.ReviewedAt).ToListAsync(cancellationToken);
        var reviewDates = GetReviewDates(reviewInstants);
        var (currentStreak, longestStreak) = CalculateStreaks(reviewDates, today);

        var addedThisWeek = await items.CountAsync(x => x.CreatedAt >= weekStartUtc && x.CreatedAt < tomorrowStartUtc, cancellationToken);
        var learnedThisWeek = await items.CountAsync(x => x.Progress.ForeignToTranslationState == SrsCardState.Review && x.Progress.TranslationToForeignState == SrsCardState.Review &&
            x.Progress.UpdatedAt >= weekStartUtc && x.Progress.UpdatedAt < tomorrowStartUtc, cancellationToken);
        var reviewsThisWeek = await history.CountAsync(x => x.ReviewedAt >= weekStartUtc && x.ReviewedAt < tomorrowStartUtc, cancellationToken);

        var accuracy = reviewsToday == 0 ? 0 : (int)Math.Round(correctToday * 100d / reviewsToday);

        return new(total, words, total - words, fresh, total - fresh - learned, learned, due, reviewsToday, accuracy, currentStreak, longestStreak, addedThisWeek, 
            learnedThisWeek, reviewsThisWeek);
    }

    internal static (int Current, int Longest) CalculateStreaks(IReadOnlyList<DateOnly> dates, DateOnly today)
    {
        if (dates.Count == 0)
            return (0, 0);

        var dateSet = new HashSet<DateOnly>();

        foreach (var date in dates)
            dateSet.Add(date);

        var cursor = dateSet.Contains(today) ? today : today.AddDays(-1);
        var current = 0;

        while (dateSet.Contains(cursor))
        {
            current++;
            cursor = cursor.AddDays(-1);
        }

        var longest = 0;
        var run = 0;
        DateOnly? previous = null;

        foreach (var date in dates)
        {
            run = previous is not null && date == previous.Value.AddDays(1) ? run + 1 : 1;
            longest = Math.Max(longest, run);
            previous = date;
        }

        return (current, longest);
    }

    private IReadOnlyList<DateOnly> GetReviewDates(IReadOnlyList<DateTimeOffset> reviewInstants)
    {
        var dates = new HashSet<DateOnly>();

        foreach (var instant in reviewInstants)
            dates.Add(_clock.ToLocalDate(instant));

        var result = dates.ToList();
        result.Sort();

        return result;
    }
}
