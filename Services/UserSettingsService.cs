using LexiLoop.Data;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace LexiLoop.Services;

public class UserSettingsService
{
    private readonly IDbContextFactory<LexiLoopDbContext> _dbFactory;

    public UserSettingsService(IDbContextFactory<LexiLoopDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<UserSettings> GetAsync(long userId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.UserSettings.AsNoTracking().SingleAsync(x => x.UserId == userId, cancellationToken);
    }

    public async Task SetNewItemsPerDayAsync(long userId, int value, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        await db.UserSettings
            .Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(x => x.SetProperty(settings => settings.NewItemsPerDay, value), cancellationToken);
    }

    public async Task SetReviewTimeAsync(long userId, TimeOnly value, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        await db.UserSettings
            .Where(x => x.UserId == userId)
            .ExecuteUpdateAsync(x => x.SetProperty(settings => settings.ReviewNotificationTime, value), cancellationToken);
    }

    public async Task UpdateButtonSettingAsync(long userId, string setting, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var settings = await db.UserSettings.SingleAsync(x => x.UserId == userId, cancellationToken);

        if (setting == "pron")
            settings.ShowPronunciation = !settings.ShowPronunciation;
        else if (setting == "notif")
            settings.ReviewNotificationsEnabled = !settings.ReviewNotificationsEnabled;
        else if (setting == "mode")
            settings.DefaultLearningMode = (LearningMode)(((int)settings.DefaultLearningMode + 1) % 3);
        else
            return;

        await db.SaveChangesAsync(cancellationToken);
    }
}
