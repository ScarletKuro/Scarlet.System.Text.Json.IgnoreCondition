using System.Text.Json.Serialization;

namespace Scarlet.Text.Json.Serialization;

/// <summary>
/// Prevents a property or field from being serialized or deserialized under the specified condition,
/// including the read-side conditions that the built-in
/// <see cref="global::System.Text.Json.Serialization.JsonIgnoreAttribute"/> cannot express.
/// </summary>
/// <remarks>
/// <para>
/// The attribute only takes effect once <see cref="ExtendedIgnoreConditionResolver"/> has been set as
/// the <see cref="global::System.Text.Json.JsonSerializerOptions.TypeInfoResolver"/>.
/// </para>
/// <para>
/// The attribute may be applied twice to combine one read-side condition with one write-side
/// condition. Two conditions on the same side throws.
/// </para>
/// <para>
/// On a positional record parameter, target the generated property explicitly with
/// <c>[property: JsonIgnoreEx(...)]</c>: an attribute on the parameter itself would belong to the
/// parameter, which the contract never surfaces.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
/// [JsonIgnoreEx(JsonIgnoreConditionEx.WhenWritingNull)]
/// public string? Name { get; set; } = "initial";
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public sealed class JsonIgnoreExAttribute : JsonAttribute
{
    /// <summary>
    /// Initializes a new instance whose <see cref="Condition"/> is
    /// <see cref="JsonIgnoreConditionEx.Always"/>.
    /// </summary>
    public JsonIgnoreExAttribute()
        : this(JsonIgnoreConditionEx.Always)
    {
    }

    /// <summary>Initializes a new instance with the specified condition.</summary>
    /// <param name="condition">The condition under which the member is ignored.</param>
    public JsonIgnoreExAttribute(JsonIgnoreConditionEx condition) => Condition = condition;

    /// <summary>Gets the condition under which the member is ignored.</summary>
    public JsonIgnoreConditionEx Condition { get; }
}
