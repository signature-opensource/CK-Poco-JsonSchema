using Json.Schema;
using Shouldly;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CK.Poco.Exc.Json.Schema.Tests;

/// <summary>
/// Validates JSON texts against a part of a generated document (JsonSchema.Net, draft 2020-12).
/// </summary>
sealed class SchemaValidator
{
    readonly JsonSchema _schema;

    SchemaValidator( string document, string pointer )
    {
        var root = JsonNode.Parse( document )!.AsObject();
        root["$ref"] = "#" + pointer;
        _schema = JsonSchema.FromText( root.ToJsonString() );
    }

    /// <summary>
    /// Gets a validator of a definition of the last captured schema (<see cref="SchemaCapture.Last"/>).
    /// </summary>
    /// <param name="definition">The name of the definition (the key in <c>$defs</c>).</param>
    /// <param name="property">Optional property of the definition (a camelCase name).</param>
    public static SchemaValidator Of( string definition, string? property = null )
    {
        var document = Encoding.UTF8.GetString( SchemaCapture.Last.ShouldNotBeNull().Utf8Document.Span );
        var pointer = "/$defs/" + System.Uri.EscapeDataString( definition.Replace( "~", "~0" ).Replace( "/", "~1" ) );
        if( property != null ) pointer += "/properties/" + property;
        return new SchemaValidator( document, pointer );
    }

    /// <summary>
    /// Gets whether the JSON is valid.
    /// </summary>
    public bool IsValid( string json )
    {
        using var d = JsonDocument.Parse( json );
        return _schema.Evaluate( d.RootElement ).IsValid;
    }

    public void ShouldAccept( string json ) => IsValid( json ).ShouldBeTrue( $"The schema must accept: {json}" );

    public void ShouldReject( string json ) => IsValid( json ).ShouldBeFalse( $"The schema must reject: {json}" );
}
