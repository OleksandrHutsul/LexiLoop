using LexiLoop.Data;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using LexiLoop.Services.Interfaces;
using LexiLoop.Services.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;

namespace LexiLoop.Services;

public class CollectionService
{
    private readonly IDbContextFactory<LexiLoopDbContext> _dbFactory;
    private readonly IAppClock _clock;

    public CollectionService(IDbContextFactory<LexiLoopDbContext> dbFactory, IAppClock clock)
    {
        _dbFactory = dbFactory;
        _clock = clock;
    }

    public async Task<CollectionListPage> GetListPageAsync(long userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Collections.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id);

        var total = await query.CountAsync(cancellationToken);
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pageCount);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CollectionSummary(x.Id, x.Name, x.VocabularyItems.Count))
            .ToListAsync(cancellationToken);

        return new(items, page, pageCount, total);
    }

    public async Task<CollectionPage?> GetAsync(long userId, long collectionId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var collection = await db.Collections.AsNoTracking()
            .Where(x => x.Id == collectionId && x.UserId == userId)
            .Select(x => new { x.Id, x.Name, Total = x.VocabularyItems.Count })
            .SingleOrDefaultAsync(cancellationToken);

        if (collection is null)
            return null;

        var pageCount = Math.Max(1, (int)Math.Ceiling(collection.Total / (double)pageSize));
        page = Math.Clamp(page, 1, pageCount);

        var items = await db.VocabularyItemCollections.AsNoTracking()
            .Where(x => x.CollectionId == collectionId && x.Collection.UserId == userId)
            .Select(x => x.VocabularyItem)
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new(collection.Id, collection.Name, items, page, pageCount, collection.Total);
    }

    public async Task<CollectionMembershipPage?> GetMembershipPageAsync(long userId, long collectionId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var collection = await db.Collections.AsNoTracking()
            .Where(x => x.Id == collectionId && x.UserId == userId)
            .Select(x => new { x.Id, x.Name })
            .SingleOrDefaultAsync(cancellationToken);

        if (collection is null)
            return null;

        var query = db.VocabularyItems.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Id);

        var total = await query.CountAsync(cancellationToken);
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pageCount);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CollectionMembershipItem(x.Id, x.ForeignText, x.Translation, x.Collections.Any(link => link.CollectionId == collectionId)))
            .ToListAsync(cancellationToken);

        return new(collection.Id, collection.Name, items, page, pageCount, total);
    }

    public async Task<ItemCollectionMembershipPage?> GetItemMembershipPageAsync(long userId, long vocabularyItemId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var itemExists = await db.VocabularyItems.AsNoTracking()
            .AnyAsync(x => x.Id == vocabularyItemId && x.UserId == userId, cancellationToken);

        if (!itemExists)
            return null;

        var query = db.Collections.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id);

        var total = await query.CountAsync(cancellationToken);
        var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pageCount);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ItemCollectionMembership(x.Id, x.Name, x.VocabularyItems.Any(link => link.VocabularyItemId == vocabularyItemId)))
            .ToListAsync(cancellationToken);

        return new(vocabularyItemId, items, page, pageCount, total);
    }

    public async Task<CollectionCreateResult> CreateWithResultAsync(long userId, string name, CancellationToken cancellationToken)
    {
        name = NormalizeName(name);

        if (!IsValidName(name))
            return new(CollectionCreateStatus.InvalidName, name);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (await db.Collections.AnyAsync(x => x.UserId == userId && x.Name == name, cancellationToken))
            return new(CollectionCreateStatus.DuplicateName, name);

        var now = _clock.UtcNow;
        var collection = new Collection
        {
            UserId = userId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Collections.Add(collection);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new(CollectionCreateStatus.Created, name, collection.Id);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new(CollectionCreateStatus.DuplicateName, name);
        }
    }

    public async Task<bool> RenameAsync(long userId, long collectionId, string name, CancellationToken cancellationToken)
    {
        name = NormalizeName(name);

        if (!IsValidName(name))
            return false;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var collection = await db.Collections.SingleOrDefaultAsync(x => x.Id == collectionId && x.UserId == userId, cancellationToken);

        if (collection is null)
            return false;

        var duplicateExists = await db.Collections.AnyAsync(x => x.UserId == userId && x.Id != collectionId && x.Name == name, cancellationToken);

        if (duplicateExists)
            return false;

        collection.Name = name;
        collection.UpdatedAt = _clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(long userId, long collectionId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var collection = await db.Collections.SingleOrDefaultAsync(x => x.Id == collectionId && x.UserId == userId, cancellationToken);

        if (collection is null)
            return false;

        db.Collections.Remove(collection);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> AddItemAsync(long userId, long collectionId, long vocabularyItemId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var collection = await db.Collections.SingleOrDefaultAsync(x => x.Id == collectionId && x.UserId == userId, cancellationToken);
        var ownsItem = await db.VocabularyItems.AnyAsync(x => x.Id == vocabularyItemId && x.UserId == userId, cancellationToken);

        if (collection is null || !ownsItem)
            return false;

        var linkExists = await db.VocabularyItemCollections
            .AnyAsync(x => x.CollectionId == collectionId && x.VocabularyItemId == vocabularyItemId, cancellationToken);

        if (linkExists)
            return true;

        db.VocabularyItemCollections.Add(new VocabularyItemCollection
        {
            CollectionId = collectionId,
            VocabularyItemId = vocabularyItemId
        });

        collection.UpdatedAt = _clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveItemAsync(long userId, long collectionId, long vocabularyItemId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var collection = await db.Collections.SingleOrDefaultAsync(x => x.Id == collectionId && x.UserId == userId, cancellationToken);

        if (collection is null)
            return false;

        var link = await db.VocabularyItemCollections.SingleOrDefaultAsync(x => x.CollectionId == collectionId && x.VocabularyItemId == vocabularyItemId 
            && x.VocabularyItem.UserId == userId, cancellationToken);

        if (link is null)
            return false;

        db.VocabularyItemCollections.Remove(link);
        collection.UpdatedAt = _clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int?> AddUncategorizedAsync(long userId, long collectionId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var collection = await db.Collections.SingleOrDefaultAsync(x => x.Id == collectionId && x.UserId == userId, cancellationToken);

        if (collection is null)
            return null;

        var itemIds = await db.VocabularyItems
            .Where(x => x.UserId == userId && !x.Collections.Any())
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (itemIds.Count == 0)
            return 0;

        foreach (var vocabularyItemId in itemIds)
        {
            db.VocabularyItemCollections.Add(new VocabularyItemCollection
            {
                CollectionId = collectionId,
                VocabularyItemId = vocabularyItemId
            });
        }

        collection.UpdatedAt = _clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return itemIds.Count;
    }

    public async Task<CollectionShare?> GetOrCreateShareAsync(long userId, long collectionId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var collection = await db.Collections.SingleOrDefaultAsync(x => x.Id == collectionId && x.UserId == userId, cancellationToken);

        if (collection is null)
            return null;

        if (string.IsNullOrWhiteSpace(collection.ShareToken))
        {
            string token;

            do
            {
                token = CreateShareToken();
            }
            while (await db.Collections.AnyAsync(x => x.ShareToken == token, cancellationToken));

            collection.ShareToken = token;
            collection.UpdatedAt = _clock.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
        }

        var total = await db.VocabularyItemCollections.CountAsync(x => x.CollectionId == collectionId, cancellationToken);
        return new(collection.ShareToken, collection.Name, total);
    }

    public async Task<bool> RevokeShareAsync(long userId, long collectionId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var collection = await db.Collections.SingleOrDefaultAsync(x => x.Id == collectionId && x.UserId == userId, cancellationToken);

        if (collection is null)
            return false;

        collection.ShareToken = null;
        collection.UpdatedAt = _clock.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SharedCollectionPage?> GetSharedAsync(long viewerUserId, string token, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!IsValidShareToken(token))
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var collection = await db.Collections.AsNoTracking()
            .Where(x => x.ShareToken == token)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.Name,
                OwnerName = x.User.FirstName ?? (x.User.Username == null ? "a LexiLoop user" : "@" + x.User.Username),
                Total = x.VocabularyItems.Count
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (collection is null)
            return null;

        var pageCount = Math.Max(1, (int)Math.Ceiling(collection.Total / (double)pageSize));
        page = Math.Clamp(page, 1, pageCount);

        var items = await db.VocabularyItemCollections.AsNoTracking()
            .Where(x => x.CollectionId == collection.Id)
            .Select(x => x.VocabularyItem)
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new(token, collection.Name, collection.OwnerName, items, page, pageCount, collection.Total, collection.UserId == viewerUserId);
    }

    public async Task<SharedCollectionImportResult> ImportSharedAsync(long recipientUserId, string token, CancellationToken cancellationToken)
    {
        if (!IsValidShareToken(token))
            return new(SharedCollectionImportStatus.InvalidToken);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var source = await db.Collections.AsNoTracking()
            .Where(x => x.ShareToken == token)
            .Select(x => new { x.Id, x.UserId, x.Name })
            .SingleOrDefaultAsync(cancellationToken);

        if (source is null)
            return new(SharedCollectionImportStatus.InvalidToken);

        if (source.UserId == recipientUserId)
            return new(SharedCollectionImportStatus.OwnCollection);

        var previousImport = await db.Collections.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == recipientUserId && x.ImportedFromShareToken == token, cancellationToken);

        if (previousImport is not null)
            return new(SharedCollectionImportStatus.AlreadyImported, previousImport.Id, previousImport.Name);

        var sourceItems = await db.VocabularyItemCollections.AsNoTracking()
            .Where(x => x.CollectionId == source.Id)
            .Select(x => x.VocabularyItem)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var recipientItems = await db.VocabularyItems
            .Where(x => x.UserId == recipientUserId)
            .ToListAsync(cancellationToken);

        var recipientByText = new Dictionary<string, VocabularyItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var recipientItem in recipientItems)
            recipientByText.TryAdd(ItemKey(recipientItem), recipientItem);

        var existingNames = await db.Collections.AsNoTracking()
            .Where(x => x.UserId == recipientUserId)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        var copy = new Collection
        {
            UserId = recipientUserId,
            Name = UniqueImportedName(source.Name, existingNames),
            ImportedFromShareToken = token,
            CreatedAt = now,
            UpdatedAt = now
        };

        var added = 0;
        var reused = 0;
        var copiedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceItem in sourceItems)
        {
            var itemKey = ItemKey(sourceItem);

            if (!copiedKeys.Add(itemKey))
                continue;

            if (!recipientByText.TryGetValue(itemKey, out var target))
            {
                target = new VocabularyItem
                {
                    UserId = recipientUserId,
                    ForeignText = sourceItem.ForeignText,
                    Translation = sourceItem.Translation,
                    Pronunciation = sourceItem.Pronunciation,
                    Type = sourceItem.Type,
                    IsLearningEnabled = false,
                    IsFavorite = false,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Progress = new LearningProgress
                    {
                        CreatedAt = now,
                        UpdatedAt = now
                    }
                };

                recipientByText[itemKey] = target;
                added++;
            }
            else
            {
                reused++;
            }

            copy.VocabularyItems.Add(new VocabularyItemCollection
            {
                Collection = copy,
                VocabularyItem = target
            });
        }

        db.Collections.Add(copy);
        await db.SaveChangesAsync(cancellationToken);

        return new(SharedCollectionImportStatus.Imported, copy.Id, copy.Name, added, reused);
    }

    private static string CreateShareToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool IsValidShareToken(string token)
    {
        if (token.Length != 32)
            return false;

        foreach (var character in token)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')
                return false;
        }

        return true;
    }

    private static string ItemKey(VocabularyItem item)
    {
        return $"{item.ForeignText.Trim()}\u001f{item.Translation.Trim()}";
    }

    private static string UniqueImportedName(string sourceName, IReadOnlyCollection<string> existingNames)
    {
        var names = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!names.Contains(sourceName))
            return sourceName;

        for (var number = 2; ; number++)
        {
            var suffix = $" ({number})";
            var prefix = sourceName.Length + suffix.Length <= 100 ? sourceName : sourceName[..(100 - suffix.Length)];
            var candidate = prefix + suffix;

            if (!names.Contains(candidate))
                return candidate;
        }
    }

    private static string NormalizeName(string name)
    {
        return name.Trim();
    }

    private static bool IsValidName(string name)
    {
        return name.Length is > 0 and <= 100;
    }
}
