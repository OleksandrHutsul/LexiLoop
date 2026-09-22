using LexiLoop.Data;
using LexiLoop.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace LexiLoop.Services;

public sealed class ReviewNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotClient _bot;
    private readonly IAppClock _clock;
    private readonly ILogger<ReviewNotificationWorker> _logger;

    public ReviewNotificationWorker(IServiceScopeFactory scopeFactory, ITelegramBotClient bot,
        IAppClock clock, ILogger<ReviewNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _bot = bot;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await NotifyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Review notification cycle failed");
            }
        }
    }

    private async Task NotifyAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LexiLoopDbContext>>();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        var now = _clock.UtcNow;
        var today = _clock.LocalToday;

        var users = await db.Users
            .Include(x => x.Settings)
            .Where(x => x.Settings.ReviewNotificationsEnabled && x.Settings.LastNotificationDate != today)
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            var scheduledAtUtc = _clock.GetUtcInstant(today, user.Settings.ReviewNotificationTime);

            if (now < scheduledAtUtc)
                continue;

            var due = await db.VocabularyItems.CountAsync(x => x.UserId == user.Id && x.IsLearningEnabled && 
                ((x.Progress.ForeignToTranslationReviewCount > 0 && x.Progress.ForeignToTranslationNextReviewAt <= now) ||
                 (x.Progress.TranslationToForeignReviewCount > 0 && x.Progress.TranslationToForeignNextReviewAt <= now)), cancellationToken);

            if (due == 0)
                continue;

            var keyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Start review", "choose:review"));

            await _bot.SendMessage(user.TelegramUserId, $"🧠 Time to review!\n\nYou have {due} items waiting for review.", replyMarkup: keyboard, cancellationToken: cancellationToken);

            user.Settings.LastNotificationDate = today;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
