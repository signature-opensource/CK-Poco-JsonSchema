# CK.Poco.Exc.Json.Schema

The JSON Schema of the Pocos describes the JSON that the Poco JSON exchange (CK.Poco.Exc.Json) writes and reads: the
wire format, not the C# types. Its users are the description of the commands of a party, OpenAPI documents, and the
tests that check the serialized Pocos against it.

This package is the opt-in: referencing it registers `CommonPocoJsonSchemaSupport`, which triggers the engine
([CK.Poco.Exc.Json.Schema.Engine](../CK.Poco.Exc.Json.Schema.Engine/README.md)) and registers the Poco JSON exchange
(`CommonPocoJsonSupport`) with it.
