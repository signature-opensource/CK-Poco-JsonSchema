using System;

namespace CK.Setup.PocoJsonSchema;

/// <summary>
/// The JSON Schema (draft 2020-12) of a type set: a self-contained document whose <c>$defs</c> are the named types of the
/// set (the Pocos, the named records and the enums) under their exchange name.
/// </summary>
public sealed class PocoJsonSchema
{
    internal PocoJsonSchema( IPocoTypeSet typeSet, ReadOnlyMemory<byte> utf8Document, string signature )
    {
        TypeSet = typeSet;
        Utf8Document = utf8Document;
        Signature = signature;
    }

    /// <summary>
    /// Gets the type set described by this schema.
    /// </summary>
    public IPocoTypeSet TypeSet { get; }

    /// <summary>
    /// Gets the UTF-8 bytes of the document (indented, without BOM).
    /// </summary>
    public ReadOnlyMemory<byte> Utf8Document { get; }

    /// <summary>
    /// Gets the SHA-256 of <see cref="Utf8Document"/>, as a lowercase hexadecimal string.
    /// </summary>
    public string Signature { get; }
}
