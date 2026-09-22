using LexiLoop.Data;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using LexiLoop.Services.Interfaces;
using LexiLoop.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace LexiLoop.Services;

public class VocabularyService
{
    private readonly IDbContextFactory<LexiLoopDbContext> _dbFactory;
    private readonly IAppClock _clock;

    public VocabularyService(IDbContextFactory<LexiLoopDbContext> dbFactory, IAppClock clock)
    {
        _dbFactory = dbFactory;
        _clock = clock;
    }

    public async Task<AddVocabularyResult> AddAsync(long userId, IEnumerable<VocabularyInput> inputs, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var now = _clock.UtcNow;
        var normalized = new List<VocabularyInput>();
        var unique = new List<VocabularyInput>();
        var uniqueKeys = new HashSet<(string Foreign, string Translation)>();
        var added = 0;
        var duplicates = 0;
        var words = 0;
        var phrases = 0;

        foreach (var input in inputs)
        {
            var foreign = input.ForeignText.Trim();
            var translation = input.Translation.Trim();

            if (foreign.Length == 0 || translation.Length == 0)
                continue;

            normalized.Add(input with
            {
                ForeignText = foreign,
                Translation = translation,
                Pronunciation = string.IsNullOrWhiteSpace(input.Pronunciation) ? null : input.Pronunciation.Trim()
            });
        }

        foreach (var input in normalized)
        {
            var key = (input.ForeignText.ToUpperInvariant(), input.Translation.ToUpperInvariant());

            if (uniqueKeys.Add(key))
                unique.Add(input);
            else
                duplicates++;
        }

        var existingRows = await db.VocabularyItems.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.ForeignText, x.Translation })
            .ToListAsync(cancellationToken);

        var existing = new HashSet<(string Foreign, string Translation)>();

        foreach (var row in existingRows)
            existing.Add((row.ForeignText.ToUpperInvariant(), row.Translation.ToUpperInvariant()));

        foreach (var input in unique)
        {
            var key = (input.ForeignText.ToUpperInvariant(), input.Translation.ToUpperInvariant());

            if (!existing.Add(key))
            {
                duplicates++;
                continue;
            }

            db.VocabularyItems.Add(new VocabularyItem
            {
                UserId = userId,
                ForeignText = input.ForeignText,
                Translation = input.Translation,
                Pronunciation = input.Pronunciation,
                Type = input.Type,
                CreatedAt = now,
                UpdatedAt = now,
                IsLearningEnabled = false,
                Progress = new LearningProgress
                {
                    CreatedAt = now,
                    UpdatedAt = now
                }
            });

            added++;

            if (input.Type == VocabularyItemType.Word)
                words++;
            else
                phrases++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new AddVocabularyResult(added, duplicates, words, phrases);
    }

    public async Task<VocabularyPage?> GetPageAsync(long userId, string sourceKey, string filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.VocabularyItems.AsNoTracking()
            .Include(x => x.Progress)
            .Where(x => x.UserId == userId);

        string sourceName;

        if (sourceKey == "all")
        {
            sourceName = "Vocabulary";
        }
        else if (sourceKey == "uncategorized")
        {
            sourceName = "Uncategorized";
            query = query.Where(x => !x.Collections.Any());
        }
        else if (sourceKey == "favorites")
        {
            sourceName = "Favorites";
            query = query.Where(x => x.IsFavorite);
        }
        else if (sourceKey.StartsWith('c') && long.TryParse(sourceKey[1..], out var collectionId))
        {
            var collection = await db.Collections.AsNoTracking()
                .Where(x => x.Id == collectionId && x.UserId == userId)
                .Select(x => new { x.Id, x.Name })
                .SingleOrDefaultAsync(cancellationToken);

            if (collection is null)
                return null;

            sourceName = collection.Name;
            query = query.Where(x => x.Collections.Any(link => link.CollectionId == collection.Id));
        }
        else
        {
            return null;
        }

        var filterState = VocabularyFilter.Parse(filter);

        query = filterState.Type switch
        {
            VocabularyFilter.Words => query.Where(x => x.Type == VocabularyItemType.Word),
            VocabularyFilter.Phrases => query.Where(x => x.Type == VocabularyItemType.Phrase),
            _ => query
        };

        query = filterState.Status switch
        {
            VocabularyFilter.New => query.Where(x => x.Progress.ForeignToTranslationReviewCount == 0 && x.Progress.TranslationToForeignReviewCount == 0),
            VocabularyFilter.InProgress => query.Where(x => (x.Progress.ForeignToTranslationReviewCount > 0 || x.Progress.TranslationToForeignReviewCount > 0) &&
                (x.Progress.ForeignToTranslationState != SrsCardState.Review || x.Progress.TranslationToForeignState != SrsCardState.Review)),
            VocabularyFilter.Learned => query.Where(x => x.Progress.ForeignToTranslationState == SrsCardState.Review && x.Progress.TranslationToForeignState == SrsCardState.Review),
            _ => query
        };

        // Compatibility for old inline keyboards which encoded Favorites as a filter.
        if (sourceKey == "all" && filter.Equals("favorites", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.IsFavorite);

        var total = await query.CountAsync(cancellationToken);
        var words = await query.CountAsync(x => x.Type == VocabularyItemType.Word, cancellationToken);
        var phrases = total - words;
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));

        page = Math.Clamp(page, 1, pageCount);

        var items = await query
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new VocabularyPage(items, page, pageCount, total, words, phrases)
        {
            SourceKey = sourceKey,
            SourceName = sourceName,
            FilterKey = filterState.Key
        };
    }

    public async Task<VocabularyItem?> GetItemAsync(long userId, long vocabularyItemId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.VocabularyItems.AsNoTracking()
            .Include(x => x.Progress)
            .SingleOrDefaultAsync(x => x.Id == vocabularyItemId && x.UserId == userId, cancellationToken);
    }

    public async Task<bool?> ToggleFavoriteAsync(long userId, long vocabularyItemId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.VocabularyItems.SingleOrDefaultAsync(x => x.Id == vocabularyItemId && x.UserId == userId, cancellationToken);

        if (item is null)
            return null;

        item.IsFavorite = !item.IsFavorite;
        item.UpdatedAt = _clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return item.IsFavorite;
    }

    public async Task<bool> SetFavoriteAsync(long userId, long vocabularyItemId, bool isFavorite, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.VocabularyItems.SingleOrDefaultAsync(x => x.Id == vocabularyItemId && x.UserId == userId, cancellationToken);

        if (item is null)
            return false;

        if (item.IsFavorite == isFavorite)
            return true;

        item.IsFavorite = isFavorite;
        item.UpdatedAt = _clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
