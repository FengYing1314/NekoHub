using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NekoHub.Infrastructure.Persistence;

internal static class SqliteDateTimeOffsetConverters
{
    public static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetToUtcTicks =
        new(
            value => value.UtcDateTime.Ticks,
            value => new DateTimeOffset(new DateTime(value, DateTimeKind.Utc)));

    public static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetToUtcTicks =
        new(
            value => value.HasValue ? value.Value.UtcDateTime.Ticks : null,
            value => value.HasValue
                ? new DateTimeOffset(new DateTime(value.Value, DateTimeKind.Utc))
                : null);
}
