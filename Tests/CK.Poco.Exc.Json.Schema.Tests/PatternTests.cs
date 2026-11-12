using CK.Core;
using CK.Poco.Exc.Json;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Schema.Tests;

/// <summary>
/// The basic types written as strings have a pattern: what it matches, the importer must accept (the schema is never
/// more lenient than the importer), and what the export writes, it must match.
/// </summary>
[TestFixture]
public class PatternTests
{
    [ExternalName( "Basics" )]
    public interface IBasics : IPoco
    {
        long PLong { get; set; }
        ulong PULong { get; set; }
        decimal PDecimal { get; set; }
        TimeSpan PTimeSpan { get; set; }
        DateTime PDateTime { get; set; }
        DateTimeOffset PDateTimeOffset { get; set; }
        Guid PGuid { get; set; }
        char PChar { get; set; }
    }

    static readonly string[] _integers =
    [
        "0", "-0", "1", "-1", "00", "01", "+1", " 1", "1.0", "1e2", "",
        "9223372036854775807", "9223372036854775808", "-9223372036854775808", "-9223372036854775809",
        "9223372036854775799", "9223372036854775817", "9300000000000000000", "8999999999999999999",
        "18446744073709551615", "18446744073709551616", "18446744073709551609", "18446744073709551625",
        "19000000000000000000", "99999999999999999999", "100000000000000000000"
    ];

    static readonly string[] _decimals =
    [
        "0", "-0", "1.5", "-1.5", "1.", ".5", "0.0000000000000000000000000000000000001", "1e2",
        "79228162514264337593543950335", "-79228162514264337593543950335", "79228162514264337593543950336",
        "79228162514264337593543950334.9", "79228162514264337593543950334.99999999999", "79228162514264337593543950335.5",
        "79228162514264337593543950335.4", "80000000000000000000000000000", "123456789012345678901234567890"
    ];

    static readonly string[] _dates =
    [
        "2020-01-01", "2020-02-29", "2021-02-29", "1900-02-29", "2000-02-29", "2400-02-29", "0004-02-29", "0000-02-29",
        "0000-01-01", "0001-01-01", "9999-12-31", "2020-13-01", "2020-00-01", "2020-04-31", "2020-01-32", "20200101",
        "2020-01-01T10:00", "2020-01-01T10:00Z", "2020-01-01T10:00:00", "2020-01-01T10:00:00Z", "2020-01-01T10:00:00.1",
        "2020-01-01T10:00:00.1234567", "2020-01-01T10:00:00.1234567890123456", "2020-01-01T10:00:00.12345678901234567",
        "2020-01-01T23:59:59.9999999", "2020-01-01T24:00", "2020-01-01T10:60", "2020-01-01T10:00:60", "2020-01-01t10:00",
        "2020-01-01T10:00+01:00", "2020-01-01T10:00:00-05:30", "2020-01-01T10:00+14:00", "2020-01-01T10:00-14:00",
        "2020-01-01T10:00+14:01", "2020-01-01T10:00+13:59", "2020-01-01T10:00+01", "2020-01-01Z", "2020-01-01T10",
        "0001-01-01T00:00+01:00", "0001-01-01T00:00-01:00", "0001-01-01T00:00Z", "0001-01-01T00:00:00.0000001+00:01",
        "9999-12-31T23:59-01:00", "9999-12-31T23:59+01:00", "9999-12-31T23:59:59.9999999Z", "9999-12-31T23:59:59.9999999-00:01",
        "0001-01-02T00:00+14:00", "9999-12-30T23:59-14:00"
    ];

    static readonly string[] _guids =
    [
        "00000000-0000-0000-0000-000000000000", "ffffffff-FFFF-ffff-ffff-ffffffffffff", "{00000000-0000-0000-0000-000000000000}",
        "00000000000000000000000000000000", "0000000-0000-0000-0000-000000000000", "g0000000-0000-0000-0000-000000000000"
    ];

    static readonly string[] _chars = [ "a", "", "ab", "😀", "\uD83D", "\uDE00", "", "￿", "\u0000", "퟿" ];

    [Test]
    public async Task what_a_pattern_matches_the_importer_accepts_Async()
    {
        var (patterns, auto) = await RunAsync();
        await using var _ = auto;
        var factory = auto.Services.GetRequiredService<IPocoFactory<IBasics>>();
        int matched = 0;
        var refused = new List<string>();
        Check( nameof( IBasics.PLong ), _integers );
        Check( nameof( IBasics.PULong ), _integers );
        Check( nameof( IBasics.PTimeSpan ), _integers );
        Check( nameof( IBasics.PDecimal ), _decimals.Concat( _integers ) );
        Check( nameof( IBasics.PDateTime ), _dates );
        Check( nameof( IBasics.PDateTimeOffset ), _dates );
        Check( nameof( IBasics.PGuid ), _guids );
        Check( nameof( IBasics.PChar ), _chars );
        refused.ShouldBeEmpty( "The patterns match them: the importer must accept them." );
        matched.ShouldBeGreaterThan( 50 );

        void Check( string property, IEnumerable<string> candidates )
        {
            var field = JsonNamingPolicy.CamelCase.ConvertName( property );
            var regex = new Regex( patterns[field], RegexOptions.CultureInvariant );
            foreach( var c in candidates )
            {
                if( !regex.IsMatch( c ) ) continue;
                ++matched;
                var json = $$"""{"{{field}}":{{JsonSerializer.Serialize( c )}}}""";
                try
                {
                    factory.ReadJson( json );
                }
                catch( Exception )
                {
                    refused.Add( $"{field}: '{c}'" );
                }
            }
        }
    }

    [Test]
    public async Task what_the_export_writes_the_patterns_match_Async()
    {
        var (patterns, auto) = await RunAsync();
        await using var _ = auto;
        var factory = auto.Services.GetRequiredService<IPocoFactory<IBasics>>();
        var values = new List<IBasics>
        {
            factory.Create(),
            factory.Create( o =>
            {
                o.PLong = long.MaxValue; o.PULong = ulong.MaxValue; o.PDecimal = decimal.MaxValue; o.PTimeSpan = TimeSpan.MaxValue;
                o.PDateTime = DateTime.MaxValue; o.PDateTimeOffset = DateTimeOffset.MaxValue; o.PGuid = Guid.NewGuid(); o.PChar = '￿';
            } ),
            factory.Create( o =>
            {
                o.PLong = long.MinValue; o.PDecimal = decimal.MinValue; o.PTimeSpan = TimeSpan.MinValue;
                o.PDateTime = new DateTime( 2024, 2, 29, 12, 30, 15, DateTimeKind.Utc ).AddTicks( 1234567 );
                o.PDateTimeOffset = new DateTimeOffset( 2024, 2, 29, 12, 30, 15, TimeSpan.FromHours( -5.5 ) );
                o.PChar = 'é';
            } ),
            factory.Create( o =>
            {
                o.PDecimal = 0.0000000000000000000000000001m;
                o.PDateTime = new DateTime( 2024, 1, 1, 0, 0, 0, DateTimeKind.Local );
                o.PDateTimeOffset = DateTimeOffset.MinValue;
            } ),
        };
        foreach( var o in values )
        {
            var exported = JsonNode.Parse( o.ToString( PocoJsonExportOptions.Default ) )!.AsObject();
            foreach( var (field, pattern) in patterns )
            {
                var s = exported[field]!.GetValue<string>();
                Regex.IsMatch( s, pattern, RegexOptions.CultureInvariant ).ShouldBeTrue( $"The export of '{field}' is '{s}': the pattern must match it." );
            }
        }
    }

    static async Task<(Dictionary<string, string> Patterns, AutomaticServices Auto)> RunAsync()
    {
        SchemaCapture.Last = null;
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSchemaSupport ), typeof( CaptureExchangeableSchema ), typeof( IBasics ) );
        var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var doc = JsonNode.Parse( Encoding.UTF8.GetString( SchemaCapture.Last.ShouldNotBeNull().Utf8Document.Span ) )!;
        var properties = doc["$defs"]!["Basics"]!["properties"]!.AsObject();
        return (properties.ToDictionary( kv => kv.Key, kv => kv.Value!["pattern"]!.GetValue<string>() ), auto);
    }
}
