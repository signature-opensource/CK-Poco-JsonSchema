using System;
using System.Text;

namespace CK.Setup.PocoJsonSchema;

/// <summary>
/// The regular expressions of the basic types written as strings. They are exact: a string they match is accepted by
/// the importer (the rule of the schema), and the export always writes a string they match. They use only the common
/// subset of the regular expression dialects (no lookaround, no \d that would match non ASCII digits in some dialects).
/// </summary>
static class Patterns
{
    /// <summary>
    /// A <c>long</c> (or the ticks of a <c>TimeSpan</c>): <c>Int64.Parse</c> accepts it.
    /// </summary>
    public static readonly string Int64 = $"^(?:{UpTo( "9223372036854775807" )}|-{UpTo( "9223372036854775808" )})$";

    /// <summary>
    /// A <c>ulong</c>: <c>UInt64.Parse</c> accepts it.
    /// </summary>
    public static readonly string UInt64 = $"^{UpTo( "18446744073709551615" )}$";

    /// <summary>
    /// A <c>decimal</c>: below the maximal value, any fraction (rounded by <c>Decimal.Parse</c>); the maximal value
    /// without fraction (a fraction could round it up, beyond the range).
    /// </summary>
    public static readonly string Decimal = $"^-?(?:{UpTo( "79228162514264337593543950334" )}(?:\\.[0-9]+)?|79228162514264337593543950335)$";

    /// <summary>
    /// A <c>BigInteger</c>: no bound.
    /// </summary>
    public const string BigInteger = "^-?(?:0|[1-9][0-9]*)$";

    /// <summary>
    /// A <c>Guid</c>, in the "D" format that <c>Utf8JsonReader.GetGuid</c> reads.
    /// </summary>
    public const string Guid = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

    /// <summary>
    /// A <c>char</c>: one UTF-16 code unit, a surrogate excluded (a character outside the BMP is two code units).
    /// </summary>
    public const string Char = "^[\\u0000-\\uD7FF\\uE000-\\uFFFF]$";

    // The years 0002 to 9998 (0001 and 9999 are handled apart: the ends of the calendar), the days of each month,
    // the 29th of February of the leap years only (0001 and 9999 are not leap years).
    const string _middleYear = "(?:[1-8][0-9]{3}|9[0-8][0-9]{2}|99[0-8][0-9]|999[0-8]|0[1-9][0-9]{2}|00[1-9][0-9]|000[2-9])";
    const string _days31 = "(?:0[1-9]|[12][0-9]|3[01])";
    const string _days30 = "(?:0[1-9]|[12][0-9]|30)";
    const string _days28 = "(?:0[1-9]|1[0-9]|2[0-8])";
    const string _monthDay = $"(?:(?:0[13578]|1[02])-{_days31}|(?:0[469]|11)-{_days30}|02-{_days28})";
    const string _monthDayFromFebruary = $"(?:(?:0[3578]|1[02])-{_days31}|(?:0[469]|11)-{_days30}|02-{_days28})";
    const string _monthDayToNovember = $"(?:(?:0[13578]|10)-{_days31}|(?:0[469]|11)-{_days30}|02-{_days28})";
    const string _leapYear = "(?:[0-9]{2}(?:0[48]|[2468][048]|[13579][26])|(?:0[48]|[2468][048]|[13579][26])00)";
    const string _time = "T(?:[01][0-9]|2[0-3]):[0-5][0-9](?::[0-5][0-9](?:\\.[0-9]{1,16})?)?";
    const string _offset = "(?:Z|[+-](?:(?:0[0-9]|1[0-3]):[0-5][0-9]|14:00))";

    // The dates, except the first and the last edgeDays days of the calendar (0001-01-01 and 9999-12-31 for 1 day).
    static string DatesBut( int edgeDays )
    {
        string firstJanuary = edgeDays == 1 ? "(?:0[2-9]|[12][0-9]|3[01])" : "(?:0[3-9]|[12][0-9]|3[01])";
        string lastDecember = edgeDays == 1 ? "(?:0[1-9]|[12][0-9]|30)" : "(?:0[1-9]|1[0-9]|2[0-9])";
        return $"(?:{_middleYear}-{_monthDay}|{_leapYear}-02-29|0001-{_monthDayFromFebruary}|0001-01-{firstJanuary}"
               + $"|9999-{_monthDayToNovember}|9999-12-{lastDecember})";
    }

    /// <summary>
    /// A <c>DateTime</c>, in the ISO 8601 profile of <c>Utf8JsonReader.GetDateTime</c>: a valid date, optionally a time
    /// (the seconds and their fraction optional), optionally its offset.
    /// A <c>DateTime</c> with an offset is converted to the local time of the reader: within two days of the ends of the
    /// calendar (an offset and a local offset of up to 14 hours each), only "Z" (no conversion) or no offset.
    /// </summary>
    public static readonly string DateTime = $"^(?:{DatesBut( 2 )}(?:{_time}{_offset}?)?|(?:0001-01-0[12]|9999-12-3[01])(?:{_time}Z?)?)$";

    /// <summary>
    /// A <c>DateTimeOffset</c>, in the ISO 8601 profile of <c>Utf8JsonReader.GetDateTimeOffset</c>.
    /// Without offset, the local offset of the reader applies: on the first and the last day of the calendar, a time
    /// with a zero offset only (an offset could move the UTC time out of the calendar).
    /// </summary>
    public static readonly string DateTimeOffset = $"^(?:{DatesBut( 1 )}(?:{_time}{_offset}?)?|(?:0001-01-01|9999-12-31){_time}(?:Z|[+-]00:00))$";

    /// <summary>
    /// Builds the alternation of the non negative integers from 0 to <paramref name="max"/> (decimal digits, no leading zero).
    /// </summary>
    /// <param name="max">The maximal value.</param>
    /// <returns>The non anchored pattern, in a non capturing group.</returns>
    internal static string UpTo( string max )
    {
        var b = new StringBuilder( "(?:0" );
        int n = max.Length;
        // The shorter numbers.
        if( n >= 2 ) b.Append( "|[1-9]" ).Append( n > 2 ? $"[0-9]{{0,{n - 2}}}" : "" );
        // The numbers of the same length, digit by digit: the same prefix, then a smaller digit, then any digits.
        for( int i = 0; i < n; ++i )
        {
            int d = max[i] - '0';
            int low = i == 0 ? 1 : 0;
            if( d - 1 >= low )
            {
                b.Append( '|' ).Append( max, 0, i ).Append( '[' ).Append( low ).Append( '-' ).Append( d - 1 ).Append( ']' );
                int rest = n - 1 - i;
                if( rest > 0 ) b.Append( "[0-9]" ).Append( rest > 1 ? $"{{{rest}}}" : "" );
            }
        }
        return b.Append( '|' ).Append( max ).Append( ')' ).ToString();
    }
}
