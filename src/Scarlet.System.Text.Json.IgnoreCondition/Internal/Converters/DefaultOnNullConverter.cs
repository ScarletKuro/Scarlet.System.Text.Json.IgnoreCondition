using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Scarlet.System.Text.Json.IgnoreCondition.Internal.Converters;

/// <summary>
/// Substitutes a fixed value for an incoming JSON <c>null</c> instead of letting
/// <c>System.Text.Json</c> assign <c>default</c> (or throw, for a non-nullable value type).
/// </summary>
/// <remarks>
/// <para>
/// Used for members bound to a deserialization constructor parameter, where there is no setter to
/// intercept: the substituted value is the parameter's own default, so
/// <c>record Foo(string Name = "unknown")</c> keeps <c>"unknown"</c> for <c>{"Name":null}</c>. Also used,
/// with a <see langword="null"/> replacement, to keep a null away from a converter that handles null
/// itself on a settable member, whose skipping setter then discards it.
/// </para>
/// <para>
/// Non-null tokens are forwarded to the converter that would otherwise have been used: the member's
/// own, or else the one in effect for <typeparamref name="T"/> (from the options or the type). That
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

    // Set instead of _inner when the converter in effect for T converts a base type, such as a
    // JsonConverter<object> registered for int. The serializer adapts that with an internal casting
    // converter, which the public read and write entry points for the contract apply too.
    private JsonTypeInfo<T>? _typeInfo;

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

        Resolve(options);
        T? value = _inner is { } inner
            ? inner.Read(ref reader, typeToConvert, options)
            : JsonSerializer.Deserialize(ref reader, _typeInfo!);

        // WhenReadingDefault also discards an incoming value that equals the type default.
        if (_replaceTypeDefaults && EqualityComparer<T?>.Default.Equals(value, default))
        {
            return _nullReplacement;
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        Resolve(options);
        if (_inner is not { } inner)
        {
            // The serializer's own entry point writes a null exactly as it would without this wrapper.
            JsonSerializer.Serialize(writer, value, _typeInfo!);
            return;
        }

        // HandleNull also routes nulls here on the write side. Do what the serializer would have done
        // without this wrapper: let a converter that handles null write it, otherwise write null.
        if (value is null && !inner.HandleNull)
        {
            writer.WriteNullValue();
            return;
        }

        inner.Write(writer, value, options);
    }

    private void Resolve(JsonSerializerOptions options)
    {
        if (_inner is not null || _typeInfo is not null)
        {
            return;
        }

        // Benignly racy: resolving twice yields equivalent results.
        var typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
        if (typeInfo.Converter is JsonConverter<T> typed)
        {
            _inner = typed;
        }
        else
        {
            _typeInfo = typeInfo;
        }
    }
}
