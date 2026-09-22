using LexiLoop.Data;
using LexiLoop.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace LexiLoop.Services;

public class VocabularyImportService
{
    private readonly IDbContextFactory<LexiLoopDbContext> _dbFactory;

    public VocabularyImportService(IDbContextFactory<LexiLoopDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<CsvImportPreview> PrepareAsync(long userId, CsvMappedResult parsed, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existingRows = await db.VocabularyItems.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.ForeignText, x.Translation })
            .ToListAsync(cancellationToken);

        var existing = new HashSet<(string Foreign, string Translation)>();

        foreach (var row in existingRows)
            existing.Add((row.ForeignText.ToUpperInvariant(), row.Translation.ToUpperInvariant()));

        var newItems = new List<VocabularyInput>();
        var alreadyExist = 0;

        foreach (var item in parsed.Inputs)
        {
            var key = (item.ForeignText.ToUpperInvariant(), item.Translation.ToUpperInvariant());

            if (existing.Contains(key))
                alreadyExist++;
            else
                newItems.Add(item);
        }

        var previewItems = new List<VocabularyInput>();

        for (var i = 0; i < parsed.Inputs.Count && i < 3; i++)
            previewItems.Add(parsed.Inputs[i]);

        return new(parsed.TotalRows, parsed.Inputs.Count, parsed.InvalidRows, parsed.DuplicateRows, alreadyExist, newItems, previewItems, parsed.Errors);
    }
}
