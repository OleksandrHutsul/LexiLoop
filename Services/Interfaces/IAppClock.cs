namespace LexiLoop.Services.Interfaces;

public interface IAppClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly LocalToday { get; }
    DateOnly ToLocalDate(DateTimeOffset instant);
    DateTimeOffset GetUtcStartOfDay(DateOnly localDate);
    DateTimeOffset GetUtcInstant(DateOnly localDate, TimeOnly localTime);
}
