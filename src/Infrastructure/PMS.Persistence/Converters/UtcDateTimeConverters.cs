using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PMS.Persistence.Converters;

/// <summary>
/// Forces every <see cref="DateTime"/> crossing the database boundary to be UTC.
///
/// <para>SQL Server's <c>datetime2</c> stores no offset and no kind, so a value written as
/// <see cref="DateTimeKind.Utc"/> comes back as <see cref="DateTimeKind.Unspecified"/>. That
/// is not a cosmetic detail: System.Text.Json writes an Unspecified value with no <c>Z</c>
/// suffix, so <c>JoinedAt</c> went out over the wire as <c>2026-09-07T10:22:24.8199578</c>
/// while <c>ExpiresAtUtc</c> — never round-tripped, so still Kind=Utc — went out as
/// <c>2026-09-09T17:04:02Z</c>. One API, two conventions, and a browser reading the first as
/// local time is wrong by the whole UTC offset: six hours in Dhaka, and silently.</para>
///
/// <para>Reading is where the fix has to happen; writing is belt and braces. A value that
/// arrives as Local is converted rather than relabelled, so a caller who does hand us local
/// time still stores the right instant.</para>
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            toDatabase => toDatabase.Kind == DateTimeKind.Local
                ? toDatabase.ToUniversalTime()
                : DateTime.SpecifyKind(toDatabase, DateTimeKind.Utc),
            fromDatabase => DateTime.SpecifyKind(fromDatabase, DateTimeKind.Utc))
    {
    }
}

/// <summary>
/// The nullable counterpart of <see cref="UtcDateTimeConverter"/>. EF needs it declared
/// separately: <c>DateTime?</c> is a distinct CLR type, and without this the nullable
/// columns — <c>ModifiedOnUtc</c>, <c>DeletedOnUtc</c>, <c>ExpiresOnUtc</c> — would keep
/// coming back Unspecified while the non-nullable ones were fixed.
/// </summary>
public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            toDatabase => toDatabase.HasValue
                ? toDatabase.Value.Kind == DateTimeKind.Local
                    ? toDatabase.Value.ToUniversalTime()
                    : DateTime.SpecifyKind(toDatabase.Value, DateTimeKind.Utc)
                : toDatabase,
            fromDatabase => fromDatabase.HasValue
                ? DateTime.SpecifyKind(fromDatabase.Value, DateTimeKind.Utc)
                : fromDatabase)
    {
    }
}
