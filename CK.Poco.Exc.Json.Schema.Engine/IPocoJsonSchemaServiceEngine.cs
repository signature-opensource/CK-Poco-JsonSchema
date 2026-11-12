using CK.Core;

namespace CK.Setup.PocoJsonSchema;

/// <summary>
/// The service of the JSON Schema engine, available in the engine's service container (<c>CurrentRun.ServiceContainer</c>)
/// once the Poco JSON exchange engine is available. Other engines call it for their type set.
/// </summary>
public interface IPocoJsonSchemaServiceEngine
{
    /// <summary>
    /// Gets the JSON Schema of a type set: the wire format of the Poco JSON exchange, for a reader that uses this set
    /// as its runtime filter. The schema is generated once per set (a set with the same content gives the same schema).
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="typeSet">The type set. It must be a subset of the serializable types (<see cref="IPocoSerializationServiceEngine.AllSerializable"/>).</param>
    /// <returns>The schema.</returns>
    PocoJsonSchema GetSchema( IActivityMonitor monitor, IPocoTypeSet typeSet );
}
