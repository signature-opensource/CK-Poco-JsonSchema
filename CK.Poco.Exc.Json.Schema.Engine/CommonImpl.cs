using CK.CodeGen;
using CK.Core;
using CK.Setup.PocoJson;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace CK.Setup.PocoJsonSchema;

/// <summary>
/// The code generator of <see cref="CommonPocoJsonSchemaSupport"/>: it waits for the Poco JSON exchange engine
/// (<see cref="IPocoJsonSerializationServiceEngine"/>), whose type names are the names of the schema, and registers the
/// <see cref="IPocoJsonSchemaServiceEngine"/> for the other engines.
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
        c.CurrentRun.ServiceContainer.Add<IPocoJsonSchemaServiceEngine>( new ServiceEngine( json.SerializableLayer ) );
        return CSCodeGenerationResult.Success;
    }

    sealed class ServiceEngine : IPocoJsonSchemaServiceEngine
    {
        readonly IPocoSerializationServiceEngine _serialization;
        readonly List<PocoJsonSchema> _cache;

        public ServiceEngine( IPocoSerializationServiceEngine serialization )
        {
            _serialization = serialization;
            _cache = new List<PocoJsonSchema>();
        }

        public PocoJsonSchema GetSchema( IActivityMonitor monitor, IPocoTypeSet typeSet )
        {
            Throw.CheckNotNullArgument( typeSet );
            Throw.CheckArgument( "The type set must be a subset of the serializable types.", _serialization.AllSerializable.IsSupersetOf( typeSet ) );
            foreach( var s in _cache )
            {
                if( s.TypeSet.SameContentAs( typeSet ) ) return s;
            }
            var schema = PocoJsonSchemaGenerator.Generate( monitor, _serialization, typeSet );
            _cache.Add( schema );
            return schema;
        }
    }
}
