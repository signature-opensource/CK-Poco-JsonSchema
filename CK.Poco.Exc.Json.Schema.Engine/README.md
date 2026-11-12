# CK.Poco.Exc.Json.Schema.Engine

The engine side of [CK.Poco.Exc.Json.Schema](../CK.Poco.Exc.Json.Schema/README.md): it generates the JSON Schema of a
Poco type set at setup, from the Poco type system and the exchange names of the Poco JSON exchange. Nothing is
reflected at run time.

`CommonImpl` is the code generator triggered by `CommonPocoJsonSchemaSupport`. It waits for the Poco JSON exchange
engine (`IPocoJsonSerializationServiceEngine`, CK.Poco.Exc.Json.Engine), whose type names are the names of the schema,
and registers the `IPocoJsonSchemaServiceEngine` in the engine's service container.

## The service

`IPocoJsonSchemaServiceEngine.GetSchema( monitor, typeSet )` returns the `PocoJsonSchema` of a type set (a subset of the
serializable types): `Utf8Document`, the JSON Schema (draft 2020-12) document, and its `Signature`, the SHA-256 of the
bytes. It is generated once per set: a set with the same content gets the same instance.

## The document

The schema describes what the export writes with its default options, which the import always accepts: camelCase
names, every field present. The rule: **the schema never validates what the importer refuses**. It may be stricter
than the importer (exact C# names, unknown fields and the 64-bit numbers written as numbers are accepted by the importer,
not by the schema), never more lenient.

- **The named types are the `$defs`**, under their exchange name, sorted: the Pocos (`type: object`, with
  `required` and `additionalProperties: false`), the named records (objects too), the enums (`enum` of their values,
  as strings for a 64-bit underlying type). A secondary Poco is its primary. Every reference is internal
  (`#/$defs/...`): the document is self-contained.
- **The structural types are inlined:** a list, a set or an array is an `array`; a dictionary with string keys is an
  `object`, any other dictionary an `array` of `[key, value]` pairs; an anonymous record is a positional `array`
  (`prefixItems`). A nullable type adds `null`.
- **A field is present when its type is in the set:** the set plays the role of the runtime filter of the reader. In
  an anonymous record, a cell whose type is not in the set is `null` (its position is kept).
- **The basic types:** the integers are `integer` with the range of their type, a `float` a `number` with its range;
  `long`, `ulong`, `decimal`, `BigInteger`, the ticks of a `TimeSpan`, `Guid`, `DateTime`, `DateTimeOffset` and `char`
  are strings with an exact pattern (`Patterns`): the ranges of the numbers, the calendar (the days of the months, the
  leap years), one UTF-16 code unit for a `char`. They use only the common subset of the regular expression dialects.
  A `byte[]` is a base64 string (its length checked).
- **The ends of the calendar:** a `DateTime` with an offset is converted to the local time of the reader, and a
  `DateTimeOffset` without offset takes the local offset of the reader: near `0001-01-01` and `9999-12-31` the result
  depends on the time zone of the reader, and can be out of range. There, the patterns accept only the forms that are
  safe in any time zone: for a `DateTime`, within two days, no offset or "Z"; for a `DateTimeOffset`, on the first and
  the last day, a time with a zero offset. The tests check each pattern against the importer.
- **Not described yet:** the polymorphic positions (`object`, the abstract Pocos, `ExtendedCultureInfo`) and the
  Globalization types (`UserMessage`, `SimpleUserMessage`, `FormattedString`, `MCString`, `CodeString`). Their schema
  validates nothing (`not: {}`, with a `$comment`): never more lenient than the importer.
