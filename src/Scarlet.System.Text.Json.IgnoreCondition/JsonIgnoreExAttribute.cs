namespace Scarlet.Text.Json.Serialization;

/// <summary>
/// Prevents a property or field from being serialized or deserialized under the specified condition,
/// including the read-side conditions that the built-in
/// <see cref="global::System.Text.Json.Serialization.JsonIgnoreAttribute"/> cannot express.
/// </summary>
/// <remarks>
/// <para>
/// The attribute only takes effect once the extended contract resolver has been installed, via
/// <see cref="ScarletJsonIgnoreConditionExtensions.AddExtendedIgnoreConditions"/> or
/// <see cref="ScarletJsonIgnoreConditionExtensions.WithExtendedIgnoreConditions"/>.
/// </para>
/// <para>
/// The attribute may be applied twice to combine one read-side condition with one write-side
/// condition. Two conditions on the same side is an error (SJIC004).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
/// [JsonIgnoreEx(JsonIgnoreConditionEx.WhenWritingNull)]
/// public string? Name { get; set; } = "initial";
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class JsonIgnoreExAttribute : Attribute
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

    /// <summary>Gets or sets the condition under which the member is ignored.</summary>
    public JsonIgnoreConditionEx Condition { get; set; }
}
