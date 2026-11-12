using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CK.Setup.PocoJsonSchema;

/// <summary>
/// Generates the JSON Schema of a type set, in the canonical form of the export: camelCase names, every field present.
/// The rule: the schema never validates what the importer refuses (it may be stricter, never more lenient). A position
/// that is not described yet (polymorphic, Globalization types) is a schema that nothing validates.
/// </summary>
sealed class PocoJsonSchemaGenerator
{
    const string _int64Pattern = "^-?[0-9]{1,19}$";

    readonly IPocoSerializationServiceEngine _serialization;
    readonly IPocoTypeSet _typeSet;
    readonly SortedDictionary<string, JsonObject> _defs;
    readonly HashSet<IPocoType> _defined;
    int _notDescribedCount;

    PocoJsonSchemaGenerator( IPocoSerializationServiceEngine serialization, IPocoTypeSet typeSet )
    {
        _serialization = serialization;
        _typeSet = typeSet;
        _defs = new SortedDictionary<string, JsonObject>( StringComparer.Ordinal );
        _defined = new HashSet<IPocoType>();
    }

    public static PocoJsonSchema Generate( IActivityMonitor monitor, IPocoSerializationServiceEngine serialization, IPocoTypeSet typeSet )
    {
        var g = new PocoJsonSchemaGenerator( serialization, typeSet );
        foreach( var t in typeSet.NonNullableTypes )
        {
            if( t.Kind is PocoTypeKind.PrimaryPoco or PocoTypeKind.Record or PocoTypeKind.Enum )
            {
                g.EnsureDefinition( t );
            }
        }
        var document = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$defs"] = new JsonObject( g._defs.Select( kv => KeyValuePair.Create( kv.Key, (JsonNode?)kv.Value ) ) )
        };
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using( var w = new Utf8JsonWriter( buffer, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping } ) )
        {
            document.WriteTo( w );
        }
        var bytes = buffer.WrittenMemory.ToArray();
        var signature = Convert.ToHexStringLower( SHA256.HashData( bytes ) );
        if( g._notDescribedCount > 0 )
        {
            monitor.Info( $"JSON Schema of a type set of {typeSet.NonNullableTypes.Count} types: {g._notDescribedCount} positions are not described yet (polymorphic or Globalization types)." );
        }
        return new PocoJsonSchema( typeSet, bytes, signature );
    }

    string GetName( IPocoType t ) => _serialization.SerializableNames.GetName( t.NonNullable );

    bool IsInSet( IPocoType t ) => _typeSet.NonNullableTypes.Contains( t.NonNullable );

    bool IsSerializable( IPocoType t ) => _serialization.AllSerializable.NonNullableTypes.Contains( t.NonNullable );

    JsonObject Ref( IPocoType named )
    {
        EnsureDefinition( named );
        return new JsonObject { ["$ref"] = "#/$defs/" + EscapeJsonPointer( GetName( named ) ) };
    }

    static string EscapeJsonPointer( string name ) => Uri.EscapeDataString( name.Replace( "~", "~0" ).Replace( "/", "~1" ) );

    void EnsureDefinition( IPocoType t )
    {
        t = t.NonNullable;
        if( !_defined.Add( t ) ) return;
        var name = GetName( t );
        // Registered first: a recursive type references itself.
        var def = new JsonObject();
        _defs.Add( name, def );
        switch( t )
        {
            case IPrimaryPocoType poco:
                FillObject( def, poco.Fields.Where( f => f.FieldAccess != PocoFieldAccessKind.AbstractReadOnly ) );
                break;
            case IRecordPocoType record:
                FillObject( def, record.Fields );
                break;
            case IEnumPocoType e:
                FillEnum( def, e );
                break;
            default:
                Throw.NotSupportedException( t.Kind.ToString() );
                break;
        }
    }

    void FillObject( JsonObject def, IEnumerable<IPocoField> fields )
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach( var f in fields )
        {
            // Not serializable: never written. Not in the set: the runtime filter omits it.
            if( !IsSerializable( f.Type ) || !IsInSet( f.Type ) ) continue;
            var name = JsonNamingPolicy.CamelCase.ConvertName( f.Name );
            properties[name] = Schema( f.Type );
            required.Add( name );
        }
        def["type"] = "object";
        def["properties"] = properties;
        def["required"] = required;
        def["additionalProperties"] = false;
    }

    static void FillEnum( JsonObject def, IEnumPocoType e )
    {
        // The underlying values, written by the writer of the underlying type: the 64-bit ones are strings.
        bool asString = e.UnderlyingType.Type == typeof( long ) || e.UnderlyingType.Type == typeof( ulong );
        var values = new JsonArray();
        foreach( var v in e.Values.Values.Select( v => Convert.ToDecimal( v, System.Globalization.CultureInfo.InvariantCulture ) ).Distinct().Order() )
        {
            values.Add( asString ? JsonValue.Create( v.ToString( System.Globalization.CultureInfo.InvariantCulture ) ) : JsonValue.Create( v ) );
        }
        def["enum"] = values;
    }

    JsonObject Schema( IPocoType t )
    {
        var s = NonNullableSchema( t.NonNullable );
        return t.IsNullable ? MakeNullable( s ) : s;
    }

    static JsonObject MakeNullable( JsonObject s )
    {
        if( s["type"] is JsonValue type
            && !s.ContainsKey( "enum" ) && !s.ContainsKey( "const" ) && !s.ContainsKey( "$ref" ) && !s.ContainsKey( "not" ) )
        {
            s["type"] = new JsonArray( type.GetValue<string>(), "null" );
            return s;
        }
        return new JsonObject { ["anyOf"] = new JsonArray( s, new JsonObject { ["type"] = "null" } ) };
    }

    JsonObject NotDescribed( string what )
    {
        ++_notDescribedCount;
        return new JsonObject { ["$comment"] = $"Not described yet: {what}.", ["not"] = new JsonObject() };
    }

    JsonObject NonNullableSchema( IPocoType t )
    {
        if( t.IsPolymorphic ) return NotDescribed( $"the polymorphic type '{GetName( t )}'" );
        switch( t.Kind )
        {
            case PocoTypeKind.Basic:
                return BasicSchema( t );
            case PocoTypeKind.PrimaryPoco:
            case PocoTypeKind.Record:
            case PocoTypeKind.Enum:
                return Ref( t );
            case PocoTypeKind.SecondaryPoco:
                // Written and named as its primary.
                return Ref( ((ISecondaryPocoType)t).PrimaryPocoType );
            case PocoTypeKind.AnonymousRecord:
                return AnonymousRecordSchema( (IRecordPocoType)t );
            case PocoTypeKind.Array:
            case PocoTypeKind.List:
            case PocoTypeKind.HashSet:
                return EnumerableSchema( (ICollectionPocoType)t );
            case PocoTypeKind.Dictionary:
                return DictionarySchema( (ICollectionPocoType)t );
            default:
                return NotDescribed( $"the type '{GetName( t )}'" );
        }
    }

    JsonObject AnonymousRecordSchema( IRecordPocoType r )
    {
        if( !r.IsRegular ) r = r.RegularType;
        // The cells are positional: a cell whose type is not serializable is not written, a cell whose type is not in
        // the set is written as null (its position is kept).
        var cells = new JsonArray();
        foreach( var f in r.Fields )
        {
            if( !IsSerializable( f.Type ) ) continue;
            cells.Add( IsInSet( f.Type ) ? Schema( f.Type ) : new JsonObject { ["type"] = "null" } );
        }
        return new JsonObject
        {
            ["type"] = "array",
            ["prefixItems"] = cells,
            ["minItems"] = cells.Count,
            ["items"] = false
        };
    }

    JsonObject EnumerableSchema( ICollectionPocoType c )
    {
        if( c.IsAbstractReadOnly ) return NotDescribed( $"the read-only collection '{c.CSharpName}'" );
        var item = c.ItemTypes[0];
        if( c.Kind == PocoTypeKind.Array && item.Type == typeof( byte ) )
        {
            return new JsonObject { ["type"] = "string", ["contentEncoding"] = "base64", ["pattern"] = "^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$" };
        }
        return new JsonObject { ["type"] = "array", ["items"] = Schema( item ) };
    }

    JsonObject DictionarySchema( ICollectionPocoType c )
    {
        if( c.IsAbstractReadOnly ) return NotDescribed( $"the read-only collection '{c.CSharpName}'" );
        var key = c.ItemTypes[0];
        var value = c.ItemTypes[1];
        if( key.Type == typeof( string ) )
        {
            return new JsonObject { ["type"] = "object", ["additionalProperties"] = Schema( value ) };
        }
        var pair = new JsonObject
        {
            ["type"] = "array",
            ["prefixItems"] = new JsonArray( Schema( key ), Schema( value ) ),
            ["minItems"] = 2,
            ["items"] = false
        };
        return new JsonObject { ["type"] = "array", ["items"] = pair };
    }

    JsonObject BasicSchema( IPocoType t )
    {
        var type = t.Type;
        if( type == typeof( string ) ) return Typed( "string" );
        if( type == typeof( bool ) ) return Typed( "boolean" );
        if( type == typeof( int ) ) return Integer( int.MinValue, int.MaxValue );
        if( type == typeof( uint ) ) return Integer( uint.MinValue, uint.MaxValue );
        if( type == typeof( short ) ) return Integer( short.MinValue, short.MaxValue );
        if( type == typeof( ushort ) ) return Integer( ushort.MinValue, ushort.MaxValue );
        if( type == typeof( byte ) ) return Integer( byte.MinValue, byte.MaxValue );
        if( type == typeof( sbyte ) ) return Integer( sbyte.MinValue, sbyte.MaxValue );
        if( type == typeof( double ) ) return Typed( "number" );
        // A float out of its range is refused by the importer (it would be an infinity).
        if( type == typeof( float ) ) return new JsonObject { ["type"] = "number", ["minimum"] = float.MinValue, ["maximum"] = float.MaxValue };
        // The 64-bit numbers are strings (a JavaScript number loses their precision).
        if( type == typeof( long ) || type == typeof( TimeSpan ) ) return Pattern( _int64Pattern );
        if( type == typeof( ulong ) ) return Pattern( "^[0-9]{1,20}$" );
        if( type == typeof( decimal ) ) return Pattern( "^-?[0-9]+(\\.[0-9]+)?$" );
        if( type == typeof( BigInteger ) ) return Pattern( "^-?[0-9]+$" );
        if( type == typeof( Guid ) ) return Pattern( "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$" );
        if( type == typeof( DateTime ) || type == typeof( DateTimeOffset ) )
        {
            return Pattern( "^[0-9]{4}-[0-9]{2}-[0-9]{2}(T[0-9]{2}:[0-9]{2}(:[0-9]{2}(\\.[0-9]{1,16})?)?(Z|[+-][0-9]{2}:[0-9]{2})?)?$" );
        }
        if( type == typeof( char ) ) return new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = 1 };
        if( type == typeof( NormalizedCultureInfo ) ) return Typed( "string" );
        return NotDescribed( $"the type '{GetName( t )}'" );

        static JsonObject Typed( string jsonType ) => new JsonObject { ["type"] = jsonType };
        static JsonObject Integer( long min, long max ) => new JsonObject { ["type"] = "integer", ["minimum"] = min, ["maximum"] = max };
        static JsonObject Pattern( string pattern ) => new JsonObject { ["type"] = "string", ["pattern"] = pattern };
    }
}
