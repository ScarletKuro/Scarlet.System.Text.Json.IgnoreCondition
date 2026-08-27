using System.Text.Json;
using System.Text.Json.Serialization;
using Scarlet.Text.Json.Serialization;

namespace Scarlet.Text.Json.Tests;

/// <summary>How the contract for a test is resolved.</summary>
/// <remarks>
/// Every behavioural test runs against all three, and the whole suite runs a second time from the
/// ReflectionFallback project, where the compile-time registry is switched off and the attributes have
/// to be discovered by reflection instead. Results must be identical in all six combinations.
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
[JsonSerializable(typeof(ParameterRecordModel))]
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
[JsonSerializable(typeof(UnannotatedModel))]
internal sealed partial class DefaultModeContext : JsonSerializerContext;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ReferenceModel))]
[JsonSerializable(typeof(ValueModel))]
[JsonSerializable(typeof(FieldModel))]
[JsonSerializable(typeof(StructModel))]
[JsonSerializable(typeof(RecordModel))]
[JsonSerializable(typeof(ParameterRecordModel))]
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
[JsonSerializable(typeof(UnannotatedModel))]
internal sealed partial class MetadataModeContext : JsonSerializerContext;

public static class Serializers
{
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

        options.TypeInfoResolver = BaseResolverFor(configuration).WithExtendedIgnoreConditions(settings);

        return options;
    }

    /// <summary>Builds options for one configuration without the extended conditions.</summary>
    public static JsonSerializerOptions CreateUnmodified(SerializerConfiguration configuration) =>
        new() { TypeInfoResolver = BaseResolverFor(configuration) };

    private static System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver BaseResolverFor(
        SerializerConfiguration configuration) => configuration switch
        {
            SerializerConfiguration.Reflection =>
                new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
            SerializerConfiguration.SourceGenDefault => DefaultModeContext.Default,
            SerializerConfiguration.SourceGenMetadata => MetadataModeContext.Default,
            _ => throw new ArgumentOutOfRangeException(nameof(configuration)),
        };
}
