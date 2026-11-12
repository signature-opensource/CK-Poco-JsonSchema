# CK.Poco.Exc.Json.Schema.Engine

The engine side of [CK.Poco.Exc.Json.Schema](../CK.Poco.Exc.Json.Schema/README.md): it generates the JSON Schema of a
Poco type set at setup, from the Poco type system and the exchange names of the Poco JSON exchange. Nothing is
reflected at run time.

`CommonImpl` is the code generator triggered by `CommonPocoJsonSchemaSupport`. It waits for the Poco JSON exchange
engine (`IPocoJsonSerializationServiceEngine`, CK.Poco.Exc.Json.Engine), whose type names are the names of the schema.
