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
public class PolymorphismTests
{
    [CKTypeDefiner]
    [ExternalName( "Shape" )]
    public interface IShape : IPoco
    {
        int Size { get; set; }
    }

    [ExternalName( "Circle" )]
    public interface ICircle : IShape
    {
        int Radius { get; set; }
    }

    [ExternalName( "Square" )]
    public interface ISquare : IShape
    {
    }

    [ExternalName( "Holder" )]
    public interface IHolder : IPoco
    {
        IShape? Shape { get; set; }
        object? Anything { get; set; }
        IList<IShape> Shapes { get; }
        ExtendedCultureInfo? Culture { get; set; }
    }

    static async Task<(JsonObject Defs, AutomaticServices Auto)> RunAsync()
    {
        SchemaCapture.Last = null;
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSchemaSupport ), typeof( CaptureExchangeableSchema ),
                                              typeof( IShape ), typeof( ICircle ), typeof( ISquare ), typeof( IHolder ) );
        var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        var text = Encoding.UTF8.GetString( SchemaCapture.Last.ShouldNotBeNull().Utf8Document.Span );
        TestHelper.Monitor.Info( text );
        return (JsonNode.Parse( text )!["$defs"]!.AsObject(), auto);
    }

    const string _shape = "CK.Poco.Exc.Json.Schema.Tests.PolymorphismTests.IShape";

    // The type names of the tags of a oneOf (or of a single tagged alternative).
    static IEnumerable<string> TagNames( JsonNode schema )
    {
        var alternatives = schema["oneOf"]?.AsArray().Select( a => a! ) ?? [schema];
        return alternatives.Select( a => a["prefixItems"]![0]!["const"]!.GetValue<string>() );
    }

    [Test]
    public async Task an_abstract_Poco_is_the_tagged_values_of_its_implementations_Async()
    {
        var (defs, auto) = await RunAsync();
        await using var _ = auto;
        TagNames( defs[_shape]! ).ShouldBe( ["Circle", "Square"] );
        defs[_shape]!["oneOf"]![0]!.ToJsonString()
            .ShouldBe( """{"type":"array","prefixItems":[{"const":"Circle"},{"$ref":"#/$defs/Circle"}],"minItems":2,"items":false}""" );
        var holder = defs["Holder"]!["properties"]!;
        holder["shape"]!.ToJsonString().ShouldBe( $$"""{"anyOf":[{"$ref":"#/$defs/{{_shape}}"},{"type":"null"}]}""" );
        holder["shapes"]!.ToJsonString().ShouldBe( """{"type":"array","items":{"$ref":"#/$defs/""" + _shape + "\"}}" );
    }

    [Test]
    public async Task object_is_the_tagged_values_of_every_final_type_of_the_set_Async()
    {
        var (defs, auto) = await RunAsync();
        await using var _ = auto;
        TagNames( defs["object"]! ).ShouldBe( ["Circle", "ExtendedCultureInfo", "Holder", $"L({_shape}?)", "NormalizedCultureInfo", "Square", "int"] );
        defs["Holder"]!["properties"]!["anything"]!.ToJsonString().ShouldBe( """{"anyOf":[{"$ref":"#/$defs/object"},{"type":"null"}]}""" );
    }

    [Test]
    public async Task ExtendedCultureInfo_is_inlined_Async()
    {
        var (defs, auto) = await RunAsync();
        await using var _ = auto;
        var culture = defs["Holder"]!["properties"]!["culture"]!["anyOf"]![0]!;
        TagNames( culture ).ShouldBe( ["ExtendedCultureInfo", "NormalizedCultureInfo"] );
        culture["oneOf"]![0]!["prefixItems"]![1]!.ToJsonString().ShouldBe( """{"type":"string"}""" );
    }

    [Test]
    public async Task the_export_writes_the_tags_of_the_schema_Async()
    {
        var (defs, auto) = await RunAsync();
        await using var _ = auto;
        var holder = auto.Services.GetRequiredService<IPocoFactory<IHolder>>().Create();
        var circle = auto.Services.GetRequiredService<IPocoFactory<ICircle>>().Create( c => c.Radius = 3 );
        var square = auto.Services.GetRequiredService<IPocoFactory<ISquare>>().Create();
        holder.Shape = circle;
        holder.Shapes.Add( square );
        holder.Shapes.Add( circle );
        holder.Culture = ExtendedCultureInfo.EnsureExtendedCultureInfo( "fr,en" );
        foreach( var anything in new object[] { 3712, square, NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr" ), new List<IShape?> { circle, null } } )
        {
            holder.Anything = anything;
            var exported = JsonNode.Parse( holder.ToString( PocoJsonExportOptions.Default ) )!;
            TagNames( defs[_shape]! ).ShouldContain( exported["shape"]![0]!.GetValue<string>() );
            foreach( var s in exported["shapes"]!.AsArray() ) TagNames( defs[_shape]! ).ShouldContain( s![0]!.GetValue<string>() );
            TagNames( defs["object"]! ).ShouldContain( exported["anything"]![0]!.GetValue<string>() );
            TagNames( defs["Holder"]!["properties"]!["culture"]!["anyOf"]![0]! ).ShouldContain( exported["culture"]![0]!.GetValue<string>() );
        }
    }
}
