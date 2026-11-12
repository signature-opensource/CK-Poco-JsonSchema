using CK.Core;
using CK.Poco.Exc.Json;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Schema.Tests;

[TestFixture]
public class SchemaTests
{
    public enum Color : byte { Red = 1, Green = 2 }

    public enum BigValue : long { Small = 1, Large = 5_000_000_000 }

    public record struct Point( int X, int Y );

    [ExternalName( "Sample" )]
    public interface ISample : IPoco
    {
        string Name { get; set; }
        int? Count { get; set; }
        long Big { get; set; }
        IList<string> Tags { get; }
        IDictionary<string, int> Map { get; }
        IDictionary<int, string> ByKey { get; }
        ref (int Id, string? Label) Pair { get; }
        Color Color { get; set; }
        BigValue Value { get; set; }
        ref Point Location { get; }
        IOther? Other { get; set; }
        byte[]? Data { get; set; }
        object? Anything { get; set; }
    }

    [ExternalName( "Other" )]
    public interface IOther : IPoco
    {
        ISample? Back { get; set; }
    }

    const string _int = """{"type":"integer","minimum":-2147483648,"maximum":2147483647}""";

    static async Task<(JsonObject Defs, AutomaticServices Auto)> RunAsync()
    {
        SchemaCapture.Last = null;
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSchemaSupport ),
                                              typeof( CaptureExchangeableSchema ),
                                              typeof( ISample ),
                                              typeof( IOther ) );
        var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var s = SchemaCapture.Last.ShouldNotBeNull();
        // The same content gives the same (cached) schema.
        SchemaCapture.Second.ShouldBeSameAs( s );
        var text = Encoding.UTF8.GetString( s.Utf8Document.Span );
        TestHelper.Monitor.Info( text );
        s.Signature.Length.ShouldBe( 64 );
        var doc = JsonNode.Parse( text )!.AsObject();
        doc["$schema"]!.GetValue<string>().ShouldBe( "https://json-schema.org/draft/2020-12/schema" );
        return (doc["$defs"]!.AsObject(), auto);
    }

    [Test]
    public async Task the_named_types_are_the_defs_under_their_exchange_name_Async()
    {
        var (defs, auto) = await RunAsync();
        await using var _ = auto;
        // Sorted by name (ordinal): a stable document.
        defs.Select( kv => kv.Key ).ShouldBe( [ "CK.Poco.Exc.Json.Schema.Tests.SchemaTests.BigValue",
                                                "CK.Poco.Exc.Json.Schema.Tests.SchemaTests.Color",
                                                "CK.Poco.Exc.Json.Schema.Tests.SchemaTests.Point",
                                                "Other",
                                                "Sample" ] );
        // The enums: their values; the 64-bit ones as strings.
        defs["CK.Poco.Exc.Json.Schema.Tests.SchemaTests.Color"]!.ToJsonString().ShouldBe( """{"enum":[1,2]}""" );
        defs["CK.Poco.Exc.Json.Schema.Tests.SchemaTests.BigValue"]!.ToJsonString().ShouldBe( """{"enum":["1","5000000000"]}""" );
        // A named record is an object.
        defs["CK.Poco.Exc.Json.Schema.Tests.SchemaTests.Point"]!.ToJsonString()
            .ShouldBe( $$"""{"type":"object","properties":{"x":{{_int}},"y":{{_int}}},"required":["x","y"],"additionalProperties":false}""" );
        // A recursive reference.
        defs["Other"]!["properties"]!["back"]!.ToJsonString().ShouldBe( """{"anyOf":[{"$ref":"#/$defs/Sample"},{"type":"null"}]}""" );
    }

    [Test]
    public async Task the_fields_of_a_Poco_Async()
    {
        var (defs, auto) = await RunAsync();
        await using var _ = auto;
        var p = defs["Sample"]!["properties"]!.AsObject();
        p["name"]!.ToJsonString().ShouldBe( """{"type":"string"}""" );
        p["count"]!.ToJsonString().ShouldBe( """{"type":["integer","null"],"minimum":-2147483648,"maximum":2147483647}""" );
        // A string with a pattern (see PatternTests).
        p["big"]!["type"]!.GetValue<string>().ShouldBe( "string" );
        p["big"]!["pattern"].ShouldNotBeNull();
        p["tags"]!.ToJsonString().ShouldBe( """{"type":"array","items":{"type":"string"}}""" );
        p["map"]!.ToJsonString().ShouldBe( $$"""{"type":"object","additionalProperties":{{_int}}}""" );
        p["byKey"]!.ToJsonString()
            .ShouldBe( """{"type":"array","items":{"type":"array","prefixItems":[""" + _int + """,{"type":"string"}],"minItems":2,"items":false}}""" );
        p["pair"]!.ToJsonString()
            .ShouldBe( $$"""{"type":"array","prefixItems":[{{_int}},{"type":["string","null"]}],"minItems":2,"items":false}""" );
        p["color"]!.ToJsonString().ShouldBe( """{"$ref":"#/$defs/CK.Poco.Exc.Json.Schema.Tests.SchemaTests.Color"}""" );
        p["data"]!["contentEncoding"]!.GetValue<string>().ShouldBe( "base64" );
        // Not described yet: nothing but null validates (never more lenient than the importer).
        p["anything"]!["anyOf"]![0]!["not"]!.ToJsonString().ShouldBe( "{}" );
        defs["Sample"]!["additionalProperties"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Test]
    public async Task the_properties_are_the_ones_the_export_writes_Async()
    {
        var (defs, auto) = await RunAsync();
        await using var _ = auto;
        var o = auto.Services.GetRequiredService<IPocoFactory<ISample>>().Create();
        var exported = JsonNode.Parse( o.ToString( PocoJsonExportOptions.Default ) )!.AsObject();
        var required = defs["Sample"]!["required"]!.AsArray().Select( n => n!.GetValue<string>() );
        exported.Select( kv => kv.Key ).ShouldBe( required );
        defs["Sample"]!["properties"]!.AsObject().Select( kv => kv.Key ).ShouldBe( required );
    }
}
