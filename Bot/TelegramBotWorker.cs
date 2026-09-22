using LexiLoop.Bot.Handlers;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;

namespace LexiLoop.Bot;

public sealed class TelegramBotWorker(
    ITelegramBotClient bot,
    TelegramUpdateHandler handler,
    ILogger<TelegramBotWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await bot.GetMe(stoppingToken);
        await bot.DeleteWebhook(dropPendingUpdates: false, cancellationToken: stoppingToken);
        await bot.SetMyCommands(new BotCommand[]
        {
            new() { Command = "today", Description = "Start today's learning session" },
            new() { Command = "add", Description = "Add vocabulary lines" },
            new() { Command = "import", Description = "Import vocabulary from JSON" },
            new() { Command = "list", Description = "Browse your vocabulary" },
            new() { Command = "learn", Description = "Learn new vocabulary" },
            new() { Command = "review", Description = "Review due vocabulary" },
            new() { Command = "collections", Description = "Manage collections" },
            new() { Command = "stats", Description = "View learning statistics" },
            new() { Command = "settings", Description = "Change learning settings" },
            new() { Command = "help", Description = "Show help" }
        }, cancellationToken: stoppingToken);
        logger.LogInformation("Starting LexiLoop as @{Username}", me.Username);
        bot.StartReceiving(handler.HandleUpdateAsync, handler.HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery], DropPendingUpdates = false }, stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
