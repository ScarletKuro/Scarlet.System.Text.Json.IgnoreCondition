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
    private readonly JsonConverter<T> _inner;

    public NullableWrapConverter(JsonConverter<T> inner) => _inner = inner;

    // The serializer handles a JSON null for Nullable<T> itself and only calls the converter for values;
    // the null checks keep the converter correct if it is ever called directly.
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return _inner.Read(ref reader, typeof(T), options);
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        _inner.Write(writer, value.Value, options);
    }
}
