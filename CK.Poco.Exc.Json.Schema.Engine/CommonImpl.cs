using CK.CodeGen;
using CK.Core;
using CK.Setup.PocoJson;
using Microsoft.Extensions.DependencyInjection;

namespace CK.Setup.PocoJsonSchema;

/// <summary>
/// The code generator of <see cref="CommonPocoJsonSchemaSupport"/>: it waits for the Poco JSON exchange engine
/// (<see cref="IPocoJsonSerializationServiceEngine"/>), whose type names are the names of the schema.
/// </summary>
public sealed class CommonImpl : ICSCodeGenerator
{
    CSCodeGenerationResult ICSCodeGenerator.Implement( IActivityMonitor monitor, ICSCodeGenerationContext ctx )
    {
        // No serialization code is generated in the Pure Unified run: nothing to describe.
        if( ctx.CurrentRun.ConfigurationGroup.IsUnifiedPure )
        {
            return CSCodeGenerationResult.Success;
        }
        return new CSCodeGenerationResult( nameof( WaitForPocoJsonSerializationServiceEngine ) );
    }

    CSCodeGenerationResult WaitForPocoJsonSerializationServiceEngine( IActivityMonitor monitor, ICSCodeGenerationContext c )
    {
        var json = c.CurrentRun.ServiceContainer.GetService<IPocoJsonSerializationServiceEngine>();
        if( json == null )
        {
            return new CSCodeGenerationResult( nameof( WaitForPocoJsonSerializationServiceEngine ) );
        }
        monitor.Info( "The Poco JSON exchange engine is available: the JSON Schema engine is ready." );
        return CSCodeGenerationResult.Success;
    }
}
