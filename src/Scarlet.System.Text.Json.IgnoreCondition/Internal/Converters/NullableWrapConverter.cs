using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scarlet.System.Text.Json.IgnoreCondition.Internal.Converters;

/// <summary>
/// Lifts a <see cref="JsonConverter{T}"/> over a non-nullable value type to one over
/// <see cref="Nullable{T}"/>.
/// </summary>
/// <remarks>
/// Needed only when a member that carries an explicit member-level converter is re-declared in the
/// contract as <c>T?</c> so that the serializer's own null handling can be reused. Without a
/// member-level converter no wrapper is required at all: the built-in <c>Nullable&lt;T&gt;</c>
/// converter is resolved from the options and keeps full fidelity.
/// </remarks>
internal sealed class NullableWrapConverter<T> : JsonConverter<T?>
    where T : struct
{
    private JsonConverter<T>? _inner;

    public NullableWrapConverter(JsonConverter<T>? inner) => _inner = inner;

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return Inner(options).Read(ref reader, typeof(T), options);
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Inner(options).Write(writer, value.Value, options);
    }

    public override T? ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Inner(options).ReadAsPropertyName(ref reader, typeof(T), options);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, [DisallowNull] T? value, JsonSerializerOptions options) =>
        Inner(options).WriteAsPropertyName(writer, value.Value, options);

    private JsonConverter<T> Inner(JsonSerializerOptions options) =>
        _inner ??= (JsonConverter<T>)options.GetTypeInfo(typeof(T)).Converter;
}
