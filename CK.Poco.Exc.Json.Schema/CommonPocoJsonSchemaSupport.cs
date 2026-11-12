using CK.Setup;

namespace CK.Core;

/// <summary>
/// Triggers the JSON Schema engine (CK.Poco.Exc.Json.Schema.Engine): referencing this package is the opt-in.
/// The Poco JSON exchange (<see cref="CommonPocoJsonSupport"/>) is registered with it: the schema describes its wire format.
/// </summary>
/// <remarks>
/// This class is not static so that it can be used as a generic argument (see CS0718).
/// </remarks>
[ContextBoundDelegation( "CK.Setup.PocoJsonSchema.CommonImpl, CK.Poco.Exc.Json.Schema.Engine" )]
[AlsoRegisterType<CommonPocoJsonSupport>]
public sealed class CommonPocoJsonSchemaSupport
{
}
