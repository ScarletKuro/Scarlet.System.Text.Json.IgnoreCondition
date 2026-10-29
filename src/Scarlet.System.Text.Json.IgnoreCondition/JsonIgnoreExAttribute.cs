using System.Text.Json.Serialization;

namespace Scarlet.System.Text.Json.IgnoreCondition;

/// <summary>Skips null or default values when deserializing a property or field.</summary>
/// <remarks>
/// Set <see cref="ExtendedIgnoreConditionResolver"/> as the serializer's resolver to enable this attribute.
/// Combine it with native <see cref="JsonIgnoreAttribute"/> write conditions. Native Always makes it
/// ineffective; native WhenReading conflicts with it. On positional records, use the property: target.
/// <para>
/// The condition is deliberately required. A bare <c>[JsonIgnoreEx]</c> would read like the native
/// <c>[JsonIgnore]</c>, which excludes the member entirely, while this attribute only filters what is read.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
/// [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
/// public string? Name { get; set; } = "initial";
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class JsonIgnoreExAttribute : JsonAttribute
{
    /// <summary>Initializes the attribute with one of the extended read conditions.</summary>
    /// <param name="condition">The read condition.</param>
    /// <exception cref="ArgumentOutOfRangeException">The condition is not defined.</exception>
    public JsonIgnoreExAttribute(JsonIgnoreConditionEx condition)
    {
        if (condition is not (JsonIgnoreConditionEx.WhenReadingNull or JsonIgnoreConditionEx.WhenReadingDefault))
        {
            throw new ArgumentOutOfRangeException(nameof(condition), condition, "Not an extended read condition.");
        }

        Condition = condition;
    }

    /// <summary>Gets the read condition.</summary>
    public JsonIgnoreConditionEx Condition { get; }
}
