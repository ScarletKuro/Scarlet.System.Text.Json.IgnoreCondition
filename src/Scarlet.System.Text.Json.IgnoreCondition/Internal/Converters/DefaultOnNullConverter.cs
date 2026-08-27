using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scarlet.Text.Json.Internal.Converters;

/// <summary>
/// Substitutes a fixed value for an incoming JSON <c>null</c> instead of letting
/// <c>System.Text.Json</c> assign <c>default</c> (or throw, for a non-nullable value type).
/// </summary>
/// <remarks>
/// <para>
/// Used for members bound to a deserialization constructor parameter, where there is no setter to
/// intercept: the substituted value is the parameter's own default, so
/// <c>record Foo(string Name = "unknown")</c> keeps <c>"unknown"</c> for <c>{"Name":null}</c>.
/// </para>
/// <para>
/// Non-null tokens are forwarded to the converter that would otherwise have been used. That
/// forwarding goes through <see cref="JsonConverter{T}.Read"/>, which is only faithful for
/// converter-based ("value") types; the caller is responsible for restricting this converter to
/// those, see <see cref="ConverterBasedTypes"/>.
/// </para>
/// </remarks>
internal sealed class DefaultOnNullConverter<T> : JsonConverter<T>
{
    private readonly T? _nullReplacement;
    private readonly bool _replaceTypeDefaults;
    private JsonConverter<T>? _inner;

    public DefaultOnNullConverter(JsonConverter<T>? inner, T? nullReplacement, bool replaceTypeDefaults)
    {
        _inner = inner;
        _nullReplacement = nullReplacement;
        _replaceTypeDefaults = replaceTypeDefaults;
    }

    /// <summary>
    /// Routes JSON <c>null</c> into <see cref="Read"/> instead of letting the serializer handle it,
    /// which is what suppresses the exception for non-nullable value types.
    /// </summary>
    public override bool HandleNull => true;

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return _nullReplacement;
        }

        T? value = Inner(options).Read(ref reader, typeToConvert, options);

        // WhenReadingDefault also discards an incoming value that equals the type default.
        if (_replaceTypeDefaults && EqualityComparer<T?>.Default.Equals(value, default))
        {
            return _nullReplacement;
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // HandleNull also routes nulls here on the write side.
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Inner(options).Write(writer, value, options);
    }

    public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Inner(options).ReadAsPropertyName(ref reader, typeToConvert, options);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, [DisallowNull] T value, JsonSerializerOptions options) =>
        Inner(options).WriteAsPropertyName(writer, value, options);

    private JsonConverter<T> Inner(JsonSerializerOptions options) =>
        // Benignly racy: resolving twice yields equivalent converters.
        _inner ??= (JsonConverter<T>)options.GetTypeInfo(typeof(T)).Converter;
}
