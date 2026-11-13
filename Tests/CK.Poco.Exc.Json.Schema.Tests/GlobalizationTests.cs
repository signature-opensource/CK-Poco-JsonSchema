using CK.Core;
using CK.Poco.Exc.Json;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Schema.Tests;

[TestFixture]
public class GlobalizationTests
{
    [ExternalName( "Messages" )]
    public interface IMessages : IPoco
    {
        FormattedString? F { get; set; }
        CodeString? C { get; set; }
        MCString? M { get; set; }
        SimpleUserMessage? S { get; set; }
        UserMessage? U { get; set; }
        object? Anything { get; set; }
    }

    static async Task<AutomaticServices> RunAsync()
    {
        SchemaCapture.Last = null;
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSchemaSupport ), typeof( CaptureExchangeableSchema ), typeof( IMessages ) );
        return (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
    }

    static IMessages CreateFull( AutomaticServices auto )
    {
        var en = NormalizedCultureInfo.EnsureNormalizedCultureInfo( "en" );
        var name = "World";
        var count = 3712;
        return auto.Services.GetRequiredService<IPocoFactory<IMessages>>().Create( m =>
        {
            m.F = new FormattedString( en, $"Hello {name}, {count} times!" );
            m.C = new CodeString( en, $"Code {name}.", "Res.Code" );
            var u = UserMessage.Warn( en, $"Beware {name}!", "Res.Beware" );
            m.U = u;
            m.M = u.Message;
            m.S = u.AsSimpleUserMessage();
            m.Anything = UserMessage.Error( en, "Boom." );
        } );
    }

    [Test]
    public async Task the_exports_validate_in_every_user_message_format_Async()
    {
        await using var auto = await RunAsync();
        var validator = SchemaValidator.Of( "Messages" );
        var empty = auto.Services.GetRequiredService<IPocoFactory<IMessages>>().Create();
        // The None messages: [0].
        var none = auto.Services.GetRequiredService<IPocoFactory<IMessages>>().Create( m => { m.S = default( SimpleUserMessage ); m.U = default( UserMessage ); } );
        var full = CreateFull( auto );
        full.F!.Value.Placeholders.Count.ShouldBe( 2 );
        foreach( var format in new[] { UserMessageSimplifiedFormat.None, UserMessageSimplifiedFormat.Simple } )
        {
            var options = new PocoJsonExportOptions { UserMessageFormat = format };
            validator.ShouldAccept( empty.ToString( options ) );
            validator.ShouldAccept( none.ToString( options ) );
            validator.ShouldAccept( full.ToString( options ) );
        }
    }

    // Candidates for each property: when the schema accepts one, the importer must accept it. The placeholders that
    // break the constraint the schema can't express are the documented exception (see below), not candidates.
    static readonly Dictionary<string, string[]> _candidates = new()
    {
        ["f"] = [ """["a","en",[]]""", """["Hello World!","en",[6,5]]""", """["a","whatever-culture",[]]""", """["a","en"]""",
                  """["a","en",[-1,1]]""", """["a",null,[]]""", "\"a\"", """["a","en",[],1]""" ],
        ["c"] = [ """["Res.A","a","en",[]]""", """["Res.A","a","en"]""", "null", """[null,"a","en",[]]""" ],
        ["m"] = [ """["a","fr","Res.A","a","en",[]]""", """["a","fr","Res.A","a","en"]""", "null" ],
        ["s"] = [ "[0]", "[0.0]", """[4,"a",0]""", """[8.0,"a",255]""", """[16,"a",256]""", """[5,"a",0]""", """[0,"a",0]""",
                  """[4,"a"]""", """[4,"a",0,1]""", "\"Info - a\"", """[4,0,"a","en","Res.A","a","en",[]]""" ],
        ["u"] = [ "[0]", """[4,"a",0]""", """[8,1,"a","fr","Res.A","a","en",[]]""", """[8,256,"a","fr","Res.A","a","en",[]]""",
                  """[8,1,"a","fr","Res.A","a","en"]""", """[3,1,"a","fr","Res.A","a","en",[]]""", "\"Warn - a\"",
                  """[16,"a",0,"b"]""" ],
        ["anything"] = [ """["UserMessage",[16,"a",0]]""", """["SimpleUserMessage",[4,"a",1]]""", """["FormattedString",["a","en",[]]]""",
                         """["UserMessage","Error - a"]""", """["MCString",["a","fr","Res.A","a","en",[]]]""" ]
    };

    [Test]
    public async Task what_the_schema_accepts_the_importer_accepts_Async()
    {
        await using var auto = await RunAsync();
        var factory = auto.Services.GetRequiredService<IPocoFactory<IMessages>>();
        var refused = new List<string>();
        int accepted = 0;
        foreach( var (property, candidates) in _candidates )
        {
            var validator = SchemaValidator.Of( "Messages", property );
            foreach( var c in candidates )
            {
                if( !validator.IsValid( c ) ) continue;
                ++accepted;
                try
                {
                    factory.ReadJson( $$"""{"{{property}}":{{c}}}""" );
                }
                catch( Exception )
                {
                    refused.Add( $"{property}: {c}" );
                }
            }
        }
        refused.ShouldBeEmpty( "The schema accepts them: the importer must accept them." );
        accepted.ShouldBeGreaterThan( 15 );
    }

    [TestCase( """["a","en",[0]]""", Description = "Not a pair." )]
    [TestCase( """["a","en",[0,2]]""", Description = "Beyond the text." )]
    [TestCase( """["abc","en",[1,1,0,1]]""", Description = "Not ordered." )]
    public async Task the_placeholders_are_the_documented_exception_Async( string formattedString )
    {
        await using var auto = await RunAsync();
        var factory = auto.Services.GetRequiredService<IPocoFactory<IMessages>>();
        var validator = SchemaValidator.Of( "Messages", "f" );
        // The schema can't express the constraint: it accepts...
        validator.ShouldAccept( formattedString );
        // ...and the importer refuses.
        Util.Invokable( () => factory.ReadJson( $$"""{"f":{{formattedString}}}""" ) ).ShouldThrow<Exception>();
        // The annotation says it.
        var doc = JsonNode.Parse( System.Text.Encoding.UTF8.GetString( SchemaCapture.Last!.Utf8Document.Span ) )!;
        doc["$defs"]!["Messages"]!["properties"]!["f"]!["prefixItems"]![2]!["x-ck-constraint"]!.GetValue<string>().ShouldBe( "placeholders" );
    }
}
