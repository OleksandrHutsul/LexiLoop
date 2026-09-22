using LexiLoop.Data;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using LexiLoop.Services.Interfaces;
using LexiLoop.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace LexiLoop.Services;

public class LearningSessionService
{
    private readonly IDbContextFactory<LexiLoopDbContext> _dbFactory;
    private readonly IReviewScheduler _scheduler;
    private readonly LearningSessionBuilder _sessionBuilder;
    private readonly IAnswerComparer _answerComparer;
    private readonly IAppClock _clock;

    public LearningSessionService(IDbContextFactory<LexiLoopDbContext> dbFactory, IReviewScheduler scheduler,
        LearningSessionBuilder sessionBuilder, IAnswerComparer answerComparer, IAppClock clock)
    {
        _dbFactory = dbFactory;
        _scheduler = scheduler;
        _sessionBuilder = sessionBuilder;
        _answerComparer = answerComparer;
        _clock = clock;
    }

    public async Task<int> CountDueAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = _clock.UtcNow;

        return await db.VocabularyItems
            .Where(x => x.UserId == userId && x.IsLearningEnabled)
            .CountAsync(x => (x.Progress.ForeignToTranslationReviewCount > 0 && x.Progress.ForeignToTranslationNextReviewAt <= now) ||
                (x.Progress.TranslationToForeignReviewCount > 0 && x.Progress.TranslationToForeignNextReviewAt <= now), cancellationToken);
    }

    public async Task<int> CountNewAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var limit = await db.UserSettings
            .Where(x => x.UserId == userId)
            .Select(x => x.NewItemsPerDay)
            .SingleAsync(cancellationToken);

        var remaining = await GetNewAllowanceAsync(db, userId, limit, cancellationToken);

        if (remaining == 0)
            return 0;

        var available = await db.VocabularyItems.CountAsync(x => x.UserId == userId && x.IsLearningEnabled 
            && (x.Progress.ForeignToTranslationReviewCount == 0 || x.Progress.TranslationToForeignReviewCount == 0), cancellationToken);

        return Math.Min(available, remaining);
    }

    public async Task<IReadOnlyList<LearningSource>> GetLearningSourcesAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var allCount = await SourceItems(db, userId, null, false).CountAsync(cancellationToken);
        var favoriteCount = await SourceItems(db, userId, null, true).CountAsync(cancellationToken);

        var collections = await db.Collections.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new LearningSource(x.Id.ToString(), x.Name, x.VocabularyItems.Count, false))
            .ToListAsync(cancellationToken);

        var sources = new List<LearningSource>
        {
            new("all", "📚 All words", allCount, true)
        };

        if (favoriteCount > 0)
            sources.Add(new LearningSource("favorites", "⭐ Favorites", favoriteCount, true));

        sources.AddRange(collections);

        return sources;
    }

    public async Task<int> CountLearnableAsync(long userId, long? collectionId, bool favorites, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await SourceItems(db, userId, collectionId, favorites).CountAsync(cancellationToken);
    }

    public async Task<int> CountLearningPoolAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await SourceItems(db, userId, null, false).CountAsync(x => x.IsLearningEnabled, cancellationToken);
    }

    public async Task<int> AddToLearningPoolAsync(long userId, long? collectionId, bool favorites, int? requestedCount, bool allItems, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await EnableLearningPoolAsync(db, userId, collectionId, favorites, requestedCount, allItems, cancellationToken);
    }

    public async Task<LearningSession?> CreateAsync(long userId, LearningSessionKind kind, LearningMode mode, long chatId, int messageId, 
        CancellationToken cancellationToken, long? collectionId = null, bool favorites = false, int? learnItemLimit = null, bool allLearnItems = false)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var now = _clock.UtcNow;
        long? sessionCollectionId = null;
        var sourceName = "All words";

        if (kind == LearningSessionKind.Learn && favorites)
        {
            sourceName = "⭐ Favorites";
        }
        else if (kind == LearningSessionKind.Learn && collectionId is not null)
        {
            var selectedCollection = await db.Collections.AsNoTracking()
                .Where(x => x.Id == collectionId && x.UserId == userId)
                .Select(x => new { x.Id, x.Name })
                .SingleOrDefaultAsync(cancellationToken);

            if (selectedCollection is null)
                return null;

            sessionCollectionId = selectedCollection.Id;
            sourceName = selectedCollection.Name;
        }

        await CancelActiveSessionsAsync(db, userId, cancellationToken);

        var settings = await db.UserSettings.AsNoTracking().SingleAsync(x => x.UserId == userId, cancellationToken);

        if (kind == LearningSessionKind.Learn && (learnItemLimit is not null || allLearnItems))
            await EnableLearningPoolAsync(db, userId, collectionId, favorites, learnItemLimit, allLearnItems, cancellationToken);

        var pool = SourceItems(db, userId, kind == LearningSessionKind.Learn ? collectionId : null, kind == LearningSessionKind.Learn && favorites)
            .Where(x => x.IsLearningEnabled);

        var dueItems = await pool
            .Include(x => x.Progress)
            .Where(x => (x.Progress.ForeignToTranslationReviewCount > 0 && x.Progress.ForeignToTranslationNextReviewAt <= now) ||
                (x.Progress.TranslationToForeignReviewCount > 0 && x.Progress.TranslationToForeignNextReviewAt <= now))
            .ToListAsync(cancellationToken);

        var items = dueItems;
        var newCardLimit = 0;

        if (kind != LearningSessionKind.Review)
        {
            var remainingNewItems = await GetNewAllowanceAsync(db, userId, settings.NewItemsPerDay, cancellationToken);
            newCardLimit = remainingNewItems;

            IQueryable<VocabularyItem> freshQuery = pool.Include(x => x.Progress);

            freshQuery = mode switch
            {
                LearningMode.ForeignToTranslation => freshQuery.Where(x => x.Progress.ForeignToTranslationReviewCount == 0),
                LearningMode.TranslationToForeign => freshQuery.Where(x => x.Progress.TranslationToForeignReviewCount == 0),
                _ => freshQuery.Where(x => x.Progress.ForeignToTranslationReviewCount == 0 ||
                    x.Progress.TranslationToForeignReviewCount == 0)
            };

            var freshItems = await freshQuery
                .OrderBy(x => x.CreatedAt)
                .Take(remainingNewItems)
                .ToListAsync(cancellationToken);

            var itemIds = new HashSet<long>();

            foreach (var item in dueItems)
                itemIds.Add(item.Id);

            foreach (var item in freshItems)
            {
                if (itemIds.Add(item.Id))
                    items.Add(item);
            }
        }

        if (items.Count == 0)
            return null;

        var cards = _sessionBuilder.Build(items, mode, kind, newCardLimit, now);

        if (cards.Count == 0)
            return null;

        var session = new LearningSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = kind,
            Mode = mode,
            CollectionId = sessionCollectionId,
            SourceName = sourceName,
            ChatId = chatId,
            MessageId = messageId,
            Status = LearningSessionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        for (var i = 0; i < cards.Count; i++)
        {
            session.Cards.Add(new LearningSessionCard
            {
                VocabularyItemId = cards[i].ItemId,
                Direction = cards[i].Direction,
                Position = i
            });
        }

        db.LearningSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(userId, session.Id, cancellationToken);
    }

    public async Task<LearningSession?> GetActiveAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var id = await db.LearningSessions
            .Where(x => x.UserId == userId && x.Status == LearningSessionStatus.Active)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return id is null ? null : await GetAsync(userId, id.Value, cancellationToken);
    }

    public async Task<LearningSession?> RevealAsync(long userId, Guid sessionId, int expectedCardIndex, int messageId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var session = await db.LearningSessions.SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId && x.Status == LearningSessionStatus.Active 
        && x.CurrentIndex == expectedCardIndex && x.MessageId == messageId && !x.AnswerRevealed, cancellationToken);

        if (session is null)
            return null;

        session.AnswerRevealed = true;
        session.UpdatedAt = _clock.UtcNow;
        session.Version++;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }

        return await GetAsync(userId, sessionId, cancellationToken);
    }

    public async Task<TypedAnswerResult?> SubmitTypedAnswerAsync(long userId, long chatId, string enteredAnswer, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var session = await db.LearningSessions
            .Include(x => x.Cards.OrderBy(c => c.Position))
            .ThenInclude(x => x.VocabularyItem)
            .ThenInclude(x => x.Progress)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.ChatId == chatId && x.Status == LearningSessionStatus.Active && !x.AnswerRevealed && x.MessageId != 0, cancellationToken);

        if (session is null || session.CurrentIndex >= session.Cards.Count)
            return null;

        var card = session.Cards.ElementAt(session.CurrentIndex);
        var expected = card.Direction == ReviewDirection.ForeignToTranslation
            ? card.VocabularyItem.Translation
            : card.VocabularyItem.ForeignText;
        var isCorrect = _answerComparer.AreEquivalent(enteredAnswer, expected);
        var previousMessageId = session.MessageId;

        card.TypedAnswerCorrect = isCorrect;

        if (!isCorrect)
        {
            card.EnteredAnswer = enteredAnswer;
            card.ExpectedAnswer = expected;
        }

        session.AnswerRevealed = true;
        session.MessageId = 0;
        session.UpdatedAt = _clock.UtcNow;
        session.Version++;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }

        var loaded = await GetAsync(userId, session.Id, cancellationToken);
        return loaded is null ? null : new(loaded, enteredAnswer, expected, isCorrect, previousMessageId);
    }

    public async Task<LearningSession?> AnswerAsync(long userId, Guid sessionId, int expectedCardIndex, int messageId, ReviewResult result, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var session = await db.LearningSessions
            .Include(x => x.Cards.OrderBy(c => c.Position))
            .ThenInclude(x => x.VocabularyItem)
            .ThenInclude(x => x.Progress)
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId && x.Status == LearningSessionStatus.Active && x.CurrentIndex == expectedCardIndex &&
                x.MessageId == messageId, cancellationToken);

        if (session is null || !session.AnswerRevealed || session.CurrentIndex >= session.Cards.Count)
            return null;

        var card = session.Cards.ElementAt(session.CurrentIndex);
        ApplyAnswer(db, session, card, result);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }

        return await GetAsync(userId, sessionId, cancellationToken);
    }

    public async Task<bool> UpdateMessageIdAsync(long userId, Guid sessionId, int expectedCardIndex, int messageId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var session = await db.LearningSessions.SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId && x.CurrentIndex == expectedCardIndex && x.MessageId == 0, cancellationToken);

        if (session is null)
            return false;

        session.MessageId = messageId;
        session.UpdatedAt = _clock.UtcNow;
        session.Version++;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        return true;
    }

    private void ApplyAnswer(LexiLoopDbContext db, LearningSession session, LearningSessionCard card, ReviewResult result)
    {
        var progress = card.VocabularyItem.Progress;
        var now = _clock.UtcNow;
        var schedule = _scheduler.Schedule(LearningSessionBuilder.GetState(progress, card.Direction), result, now);

        if (card.Direction == ReviewDirection.ForeignToTranslation)
        {
            progress.ForeignToTranslationState = schedule.State;
            progress.ForeignToTranslationStability = schedule.Stability;
            progress.ForeignToTranslationDifficulty = schedule.Difficulty;
            progress.ForeignToTranslationIntervalDays = schedule.IntervalDays;
            progress.ForeignToTranslationReviewCount = schedule.ReviewCount;
            progress.ForeignToTranslationLapseCount = schedule.LapseCount;
            progress.ForeignToTranslationLastReviewedAt = schedule.LastReviewedAt;
            progress.ForeignToTranslationNextReviewAt = schedule.NextReviewAt;
            progress.ForeignToTranslationLevel = LegacyLevel(schedule);
        }
        else
        {
            progress.TranslationToForeignState = schedule.State;
            progress.TranslationToForeignStability = schedule.Stability;
            progress.TranslationToForeignDifficulty = schedule.Difficulty;
            progress.TranslationToForeignIntervalDays = schedule.IntervalDays;
            progress.TranslationToForeignReviewCount = schedule.ReviewCount;
            progress.TranslationToForeignLapseCount = schedule.LapseCount;
            progress.TranslationToForeignLastReviewedAt = schedule.LastReviewedAt;
            progress.TranslationToForeignNextReviewAt = schedule.NextReviewAt;
            progress.TranslationToForeignLevel = LegacyLevel(schedule);
        }

        progress.ReviewCount++;

        if (result == ReviewResult.Again)
            progress.IncorrectCount++;
        else
            progress.CorrectCount++;

        progress.LastReviewedAt = now;
        progress.UpdatedAt = now;

        card.Completed = true;
        card.Result = result;

        db.ReviewHistory.Add(new ReviewHistory
        {
            VocabularyItemId = card.VocabularyItemId,
            Direction = card.Direction,
            Result = result,
            ReviewedAt = now
        });

        session.CurrentIndex++;
        session.AnswerRevealed = false;
        session.MessageId = 0;
        session.UpdatedAt = now;
        session.Version++;

        if (session.CurrentIndex >= session.Cards.Count)
            session.Status = LearningSessionStatus.Completed;
    }

    private async Task<LearningSession?> GetAsync(long userId, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.LearningSessions.AsNoTracking()
            .Include(x => x.User)
            .ThenInclude(x => x.Settings)
            .Include(x => x.Cards.OrderBy(c => c.Position))
            .ThenInclude(x => x.VocabularyItem)
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, cancellationToken);
    }

    private static int LegacyLevel(ReviewSchedule schedule)
    {
        if (schedule.State is SrsCardState.Learning or SrsCardState.Relearning)
            return 1;

        return Math.Clamp(1 + (int)Math.Log2(Math.Max(1, schedule.IntervalDays)), 1, 8);
    }

    private static IQueryable<VocabularyItem> SourceItems(LexiLoopDbContext db, long userId, long? collectionId, bool favorites)
    {
        var query = db.VocabularyItems.Where(x => x.UserId == userId);

        if (favorites)
            query = query.Where(x => x.IsFavorite);
        else if (collectionId is not null)
            query = query.Where(x => x.Collections.Any(link => link.CollectionId == collectionId.Value && link.Collection.UserId == userId));

        return query;
    }

    private static async Task<int> EnableLearningPoolAsync(LexiLoopDbContext db, long userId, long? collectionId, bool favorites, int? requestedCount, 
        bool allItems, CancellationToken cancellationToken)
    {
        var source = SourceItems(db, userId, collectionId, favorites);

        if (allItems)
        {
            var disabled = source.Where(x => !x.IsLearningEnabled);

            if (db.Database.IsRelational())
                return await disabled.ExecuteUpdateAsync(x => x.SetProperty(item => item.IsLearningEnabled, true), cancellationToken);

            var items = await disabled.ToListAsync(cancellationToken);

            foreach (var item in items)
                item.IsLearningEnabled = true;

            await db.SaveChangesAsync(cancellationToken);
            return items.Count;
        }

        var count = requestedCount ?? 10;
        var ids = await source
            .Where(x => !x.IsLearningEnabled)
            .OrderBy(x => x.CreatedAt)
            .Take(count)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var selected = db.VocabularyItems.Where(x => ids.Contains(x.Id));

        if (db.Database.IsRelational())
            return await selected.ExecuteUpdateAsync(x => x.SetProperty(item => item.IsLearningEnabled, true), cancellationToken);

        var selectedItems = await selected.ToListAsync(cancellationToken);

        foreach (var item in selectedItems)
            item.IsLearningEnabled = true;

        await db.SaveChangesAsync(cancellationToken);
        return selectedItems.Count;
    }

    private static async Task CancelActiveSessionsAsync(LexiLoopDbContext db, long userId, CancellationToken cancellationToken)
    {
        var active = db.LearningSessions.Where(x => x.UserId == userId && x.Status == LearningSessionStatus.Active);

        if (db.Database.IsRelational())
        {
            await active.ExecuteUpdateAsync(x => x
                .SetProperty(session => session.Status, LearningSessionStatus.Cancelled)
                .SetProperty(session => session.Version, session => session.Version + 1), cancellationToken);

            return;
        }

        var sessions = await active.ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.Status = LearningSessionStatus.Cancelled;
            session.Version++;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> GetNewAllowanceAsync(LexiLoopDbContext db, long userId, int dailyLimit, CancellationToken cancellationToken)
    {
        var today = _clock.LocalToday;
        var todayStart = _clock.GetUtcStartOfDay(today);
        var tomorrowStart = _clock.GetUtcStartOfDay(today.AddDays(1));

        var todayHistoryRows = await db.ReviewHistory
            .Where(x => x.VocabularyItem.UserId == userId && x.ReviewedAt >= todayStart && x.ReviewedAt < tomorrowStart)
            .GroupBy(x => new { x.VocabularyItemId, x.Direction })
            .Select(x => new
            {
                x.Key.VocabularyItemId,
                x.Key.Direction,
                Count = x.Count()
            })
            .ToListAsync(cancellationToken);

        if (todayHistoryRows.Count == 0)
            return dailyLimit;

        var itemIds = new HashSet<long>();

        foreach (var history in todayHistoryRows)
            itemIds.Add(history.VocabularyItemId);

        var directionCounts = await db.LearningProgress
            .Where(x => itemIds.Contains(x.VocabularyItemId))
            .Select(x => new
            {
                x.VocabularyItemId,
                x.ForeignToTranslationReviewCount,
                x.TranslationToForeignReviewCount
            })
            .ToListAsync(cancellationToken);

        var countsByItem = new Dictionary<long, (int ForeignToTranslation, int TranslationToForeign)>();

        foreach (var progress in directionCounts)
        {
            countsByItem[progress.VocabularyItemId] = (progress.ForeignToTranslationReviewCount, progress.TranslationToForeignReviewCount);
        }

        var introducedToday = 0;

        foreach (var history in todayHistoryRows)
        {
            if (!countsByItem.TryGetValue(history.VocabularyItemId, out var progress))
                continue;

            var srsReviewCount = history.Direction == ReviewDirection.ForeignToTranslation
                ? progress.ForeignToTranslation
                : progress.TranslationToForeign;

            if (srsReviewCount > 0 && history.Count >= srsReviewCount)
                introducedToday++;
        }

        return Math.Max(0, dailyLimit - introducedToday);
    }
}
