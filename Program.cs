using LexiLoop.Bot;
using LexiLoop.Bot.Handlers;
using LexiLoop.Data;
using LexiLoop.Options;
using LexiLoop.Services;
using LexiLoop.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddUserSecrets<SecretMarker>(optional: true);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? builder.Configuration["Telegram:BotToken"];

if (string.IsNullOrWhiteSpace(token))
    throw new InvalidOperationException("Set TELEGRAM_BOT_TOKEN or Telegram:BotToken in user secrets.");

builder.Services.AddPooledDbContextFactory<LexiLoopDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.Configure<TimeOptions>(builder.Configuration.GetSection(TimeOptions.SectionName));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAppClock, AppClock>();
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(token));

builder.Services.AddSingleton<IVocabularyParser, VocabularyParser>();
builder.Services.AddSingleton<ICsvVocabularyParser, CsvVocabularyParser>();
builder.Services.AddSingleton<IReviewScheduler, FsrsReviewScheduler>();
builder.Services.AddSingleton<IAnswerComparer, AnswerComparer>();
builder.Services.AddSingleton<LearningSessionBuilder>();

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<VocabularyService>();
builder.Services.AddScoped<VocabularyImportService>();
builder.Services.AddScoped<LearningSessionService>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddScoped<CollectionService>();
builder.Services.AddScoped<UserSettingsService>();

builder.Services.AddSingleton<ImportHandler>();
builder.Services.AddSingleton<VocabularyHandler>();
builder.Services.AddSingleton<SettingsHandler>();
builder.Services.AddSingleton<LearningHandler>();
builder.Services.AddSingleton<CollectionHandler>();
builder.Services.AddSingleton<CommandHandler>();
builder.Services.AddSingleton<TelegramUpdateHandler>();
builder.Services.AddSingleton<CollectionSharingHandler>();

builder.Services.AddHostedService<TelegramBotWorker>();
builder.Services.AddHostedService<ReviewNotificationWorker>();

await builder.Build().RunAsync();

public sealed class SecretMarker;
