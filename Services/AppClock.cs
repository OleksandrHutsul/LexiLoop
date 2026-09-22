using LexiLoop.Options;
using LexiLoop.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace LexiLoop.Services;

public class AppClock : IAppClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public AppClock(TimeProvider timeProvider, IOptions<TimeOptions> options)
    {
        _timeProvider = timeProvider;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.DefaultTimeZone);
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow().ToUniversalTime();

    public DateOnly LocalToday => ToLocalDate(UtcNow);

    public DateOnly ToLocalDate(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant.ToUniversalTime(), _timeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public DateTimeOffset GetUtcStartOfDay(DateOnly localDate)
    {
        return GetUtcInstant(localDate, TimeOnly.MinValue);
    }

    public DateTimeOffset GetUtcInstant(DateOnly localDate, TimeOnly localTime)
    {
        var local = DateTime.SpecifyKind(localDate.ToDateTime(localTime), DateTimeKind.Unspecified);

        while (_timeZone.IsInvalidTime(local))
            local = local.AddMinutes(1);

        var utc = TimeZoneInfo.ConvertTimeToUtc(local, _timeZone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
