using LexiLoop.Data;
using LexiLoop.Models.Entities;
using LexiLoop.Models.Enums;
using LexiLoop.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LexiLoop.Services;

public class UserService
{
    private readonly IDbContextFactory<LexiLoopDbContext> _dbFactory;
    private readonly IAppClock _clock;

    public UserService(IDbContextFactory<LexiLoopDbContext> dbFactory, IAppClock clock)
    {
        _dbFactory = dbFactory;
        _clock = clock;
    }

    public async Task<TelegramUser> UpsertAsync(long telegramId, string? username, string? firstName, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .Include(x => x.Settings)
            .SingleOrDefaultAsync(x => x.TelegramUserId == telegramId, cancellationToken);

        var now = _clock.UtcNow;

        if (user is null)
        {
            user = new TelegramUser
            {
                TelegramUserId = telegramId,
                Username = username,
                FirstName = firstName,
                CreatedAt = now,
                LastActivityAt = now,
                Settings = new UserSettings()
            };

            db.Users.Add(user);
        }
        else
        {
            user.Username = username;
            user.FirstName = firstName;
            user.LastActivityAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task SetInputModeAsync(long userId, UserInputMode mode, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        await db.Users
            .Where(x => x.Id == userId)
            .ExecuteUpdateAsync(x => x.SetProperty(user => user.InputMode, mode), cancellationToken);
    }
}
