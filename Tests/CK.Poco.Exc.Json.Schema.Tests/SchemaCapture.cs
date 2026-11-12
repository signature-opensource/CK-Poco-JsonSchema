using CK.CodeGen;
using CK.Core;
using CK.Setup;
using CK.Setup.PocoJsonSchema;
using Microsoft.Extensions.DependencyInjection;

namespace CK.Poco.Exc.Json.Schema.Tests;

/// <summary>
/// Registering this type captures, at engine time, the schema of the exchangeable types into <see cref="SchemaCapture.Last"/>.
/// </summary>
[ContextBoundDelegation( "CK.Poco.Exc.Json.Schema.Tests.SchemaCapture, CK.Poco.Exc.Json.Schema.Tests" )]
public sealed class CaptureExchangeableSchema
{
}

public sealed class SchemaCapture : ICSCodeGenerator
{
    public static PocoJsonSchema? Last { get; set; }

    public static PocoJsonSchema? Second { get; set; }

    CSCodeGenerationResult ICSCodeGenerator.Implement( IActivityMonitor monitor, ICSCodeGenerationContext ctx )
    {
        if( ctx.CurrentRun.ConfigurationGroup.IsUnifiedPure ) return CSCodeGenerationResult.Success;
        return new CSCodeGenerationResult( nameof( WaitForSchemaService ) );
    }

    CSCodeGenerationResult WaitForSchemaService( IActivityMonitor monitor, ICSCodeGenerationContext c )
    {
        var schema = c.CurrentRun.ServiceContainer.GetService<IPocoJsonSchemaServiceEngine>();
        if( schema == null ) return new CSCodeGenerationResult( nameof( WaitForSchemaService ) );
        var serialization = c.CurrentRun.ServiceContainer.GetService<IPocoSerializationServiceEngine>( throwOnNull: true );
        Last = schema.GetSchema( monitor, serialization.AllExchangeable );
        // Same content: the cached schema.
        Second = schema.GetSchema( monitor, serialization.AllExchangeable.Include( [] ) );
        return CSCodeGenerationResult.Success;
    }
}
