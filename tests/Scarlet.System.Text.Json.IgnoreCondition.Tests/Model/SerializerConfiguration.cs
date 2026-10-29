using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>How the contract for a test is resolved.</summary>
/// <remarks>
/// Managed tests run against all three; native tests run against both generated modes. Results must
/// agree: the resolver reads attributes from JsonPropertyInfo.AttributeProvider, which the reflection-based resolver and
/// the System.Text.Json source generator both populate.
/// </remarks>
public enum SerializerConfiguration
{
    /// <summary>The reflection-based resolver.</summary>
    Reflection,

    /// <summary>A source-generated context in the default (metadata plus fast path) mode.</summary>
    SourceGenDefault,

    /// <summary>A source-generated context in metadata-only mode.</summary>
    SourceGenMetadata,
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(ReferenceModel))]
[JsonSerializable(typeof(ValueModel))]
[JsonSerializable(typeof(FieldModel))]
[JsonSerializable(typeof(StructModel))]
[JsonSerializable(typeof(RecordModel))]
[JsonSerializable(typeof(RequiredParameterRecordModel))]
[JsonSerializable(typeof(WriteModel))]
[JsonSerializable(typeof(CombinedModel))]
[JsonSerializable(typeof(ReadingDefaultModel))]
[JsonSerializable(typeof(OptionsDefaultModel))]
[JsonSerializable(typeof(NestedModel))]
[JsonSerializable(typeof(DerivedModel))]
[JsonSerializable(typeof(RenamedModel))]
[JsonSerializable(typeof(AnimalShape))]
[JsonSerializable(typeof(GraphNode))]
[JsonSerializable(typeof(PopulateModel))]
[JsonSerializable(typeof(NumberHandlingModel))]
[JsonSerializable(typeof(CustomValueTypeModel))]
[JsonSerializable(typeof(GenericModel<string>))]
[JsonSerializable(typeof(IgnoredMemberModel))]
[JsonSerializable(typeof(UnannotatedModel))]
[JsonSerializable(typeof(ProjectedWriteDefaultsModel))]
[JsonSerializable(typeof(NonNullableReferenceModel))]
[JsonSerializable(typeof(MixedConditionModel))]
[JsonSerializable(typeof(NativePrecedenceModel))]
[JsonSerializable(typeof(ConflictingNativeReadModel))]
[JsonSerializable(typeof(JsonNameCollisionModel))]
[JsonSerializable(typeof(UnregisteredValueTypeModel))]
[JsonSerializable(typeof(UnregisteredNullableValueTypeModel))]
[JsonSerializable(typeof(IgnoreNullValuesModel))]
[JsonSerializable(typeof(IgnoreNullValuesRecord))]
[JsonSerializable(typeof(ConverterFactoryModel))]
[JsonSerializable(typeof(ConverterFactoryRecord))]
internal sealed partial class DefaultModeContext : JsonSerializerContext;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ReferenceModel))]
[JsonSerializable(typeof(ValueModel))]
[JsonSerializable(typeof(FieldModel))]
[JsonSerializable(typeof(StructModel))]
[JsonSerializable(typeof(RecordModel))]
[JsonSerializable(typeof(RequiredParameterRecordModel))]
[JsonSerializable(typeof(WriteModel))]
[JsonSerializable(typeof(CombinedModel))]
[JsonSerializable(typeof(ReadingDefaultModel))]
[JsonSerializable(typeof(OptionsDefaultModel))]
[JsonSerializable(typeof(NestedModel))]
[JsonSerializable(typeof(DerivedModel))]
[JsonSerializable(typeof(RenamedModel))]
[JsonSerializable(typeof(AnimalShape))]
[JsonSerializable(typeof(GraphNode))]
[JsonSerializable(typeof(PopulateModel))]
[JsonSerializable(typeof(NumberHandlingModel))]
[JsonSerializable(typeof(CustomValueTypeModel))]
[JsonSerializable(typeof(GenericModel<string>))]
[JsonSerializable(typeof(IgnoredMemberModel))]
[JsonSerializable(typeof(UnannotatedModel))]
[JsonSerializable(typeof(ProjectedWriteDefaultsModel))]
[JsonSerializable(typeof(NonNullableReferenceModel))]
[JsonSerializable(typeof(MixedConditionModel))]
[JsonSerializable(typeof(NativePrecedenceModel))]
[JsonSerializable(typeof(ConflictingNativeReadModel))]
[JsonSerializable(typeof(JsonNameCollisionModel))]
[JsonSerializable(typeof(UnregisteredValueTypeModel))]
[JsonSerializable(typeof(UnregisteredNullableValueTypeModel))]
[JsonSerializable(typeof(IgnoreNullValuesModel))]
[JsonSerializable(typeof(IgnoreNullValuesRecord))]
[JsonSerializable(typeof(ConverterFactoryModel))]
[JsonSerializable(typeof(ConverterFactoryRecord))]
internal sealed partial class MetadataModeContext : JsonSerializerContext;

public static class Serializers
{
    // No RegisterValueType calls: the bundled generator registers Season, Priority and Point because
    // members of those types carry [JsonIgnoreEx]. The NativeAOT run depends on that.

    // Use explicit contracts even for the managed run. The selected resolver still determines
    // whether those contracts come from reflection or source generation.
    public static T? Deserialize<T>(string json, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize(json, TypeInfo<T>(options));

    public static ValueTask<T?> DeserializeAsync<T>(
        Stream stream, JsonSerializerOptions options, CancellationToken cancellationToken = default) =>
        JsonSerializer.DeserializeAsync(stream, TypeInfo<T>(options), cancellationToken);

    public static string Serialize<T>(T value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(value, TypeInfo<T>(options));

    private static JsonTypeInfo<T> TypeInfo<T>(JsonSerializerOptions options)
    {
        options.MakeReadOnly();
        return (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
    }

    /// <summary>Builds options for one configuration with the extended conditions installed.</summary>
    public static JsonSerializerOptions Create(
        SerializerConfiguration configuration,
        Action<ExtendedIgnoreConditionSettings>? configure = null,
        Action<JsonSerializerOptions>? configureOptions = null)
    {
        var settings = new ExtendedIgnoreConditionSettings();
        configure?.Invoke(settings);

        var options = new JsonSerializerOptions();
        configureOptions?.Invoke(options);

        options.TypeInfoResolver = new ExtendedIgnoreConditionResolver(BaseResolverFor(configuration), settings);

        return options;
    }

    /// <summary>Builds options for one configuration without the extended conditions.</summary>
    public static JsonSerializerOptions CreateUnmodified(SerializerConfiguration configuration) =>
        new() { TypeInfoResolver = BaseResolverFor(configuration) };

    private static IJsonTypeInfoResolver BaseResolverFor(SerializerConfiguration configuration) =>
        configuration switch
        {
#if !NATIVE_AOT_TESTS
            SerializerConfiguration.Reflection => new DefaultJsonTypeInfoResolver(),
#endif
            SerializerConfiguration.SourceGenDefault => DefaultModeContext.Default,
            SerializerConfiguration.SourceGenMetadata => MetadataModeContext.Default,
            _ => throw new ArgumentOutOfRangeException(nameof(configuration)),
        };
}
