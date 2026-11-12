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
        defs.Select( kv => kv.Key ).ShouldBe( [ "CK.Core.IPoco",
                                                "CK.Poco.Exc.Json.Schema.Tests.SchemaTests.BigValue",
                                                "CK.Poco.Exc.Json.Schema.Tests.SchemaTests.Color",
                                                "CK.Poco.Exc.Json.Schema.Tests.SchemaTests.Point",
                                                "Other",
                                                "Sample",
                                                "object" ] );
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
        // A polymorphic position (see PolymorphismTests).
        p["anything"]!.ToJsonString().ShouldBe( """{"anyOf":[{"$ref":"#/$defs/object"},{"type":"null"}]}""" );
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

    [Test]
    public async Task the_export_validates_against_the_schema_Async()
    {
        var (_, auto) = await RunAsync();
        await using var _ = auto;
        var validator = SchemaValidator.Of( "Sample" );
        var factory = auto.Services.GetRequiredService<IPocoFactory<ISample>>();
        var empty = factory.Create();
        validator.ShouldAccept( empty.ToString( PocoJsonExportOptions.Default ) );
        var full = factory.Create( s =>
        {
            s.Name = "Full";
            s.Count = 3712;
            s.Big = long.MinValue;
            s.Tags.Add( "a" );
            s.Map.Add( "one", 1 );
            s.ByKey.Add( 2, "two" );
            s.Pair = (42, null);
            s.Color = Color.Green;
            s.Value = BigValue.Large;
            s.Location = new Point( -1, 1 );
            s.Other = auto.Services.GetRequiredService<IPocoFactory<IOther>>().Create();
            s.Data = [1, 2, 3, 4, 5];
            s.Anything = 3712;
        } );
        full.Other!.Back = full.Clone() is ISample c ? c : null;
        validator.ShouldAccept( full.ToString( PocoJsonExportOptions.Default ) );

        // What the schema refuses (the canonical form only; the importer is more lenient on some).
        var ok = JsonNode.Parse( empty.ToString( PocoJsonExportOptions.Default ) )!.AsObject();
        Reject( o => o.Remove( "name" ) );
        Reject( o => o["unknown"] = 1 );
        Reject( o => o["count"] = "x" );
        Reject( o => o["count"] = 2147483648L );
        Reject( o => o["big"] = 3 );
        Reject( o => o["big"] = "9223372036854775808" );
        Reject( o => o["color"] = 3 );
        Reject( o => o["pair"] = JsonNode.Parse( "[1]" ) );
        Reject( o => o["data"] = "abc" );
        Reject( o => o["anything"] = 3 );
        Reject( o => o["anything"] = JsonNode.Parse( """["unknown",3]""" ) );

        void Reject( System.Action<JsonObject> change )
        {
            var o = ok.DeepClone().AsObject();
            change( o );
            validator.ShouldReject( o.ToJsonString() );
        }
    }
}
