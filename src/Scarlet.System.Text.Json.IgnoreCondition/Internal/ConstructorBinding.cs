using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Scarlet.System.Text.Json.IgnoreCondition.Internal;

/// <summary>
/// The parameters of the constructor the serializer will deserialize through, if any.
/// </summary>
/// <remarks>
/// <para>
/// A member bound to a constructor parameter never goes through
/// <see cref="JsonPropertyInfo.Set"/>, so a read-side condition has to be applied by intercepting the
/// converter instead. <see cref="JsonPropertyInfo.AssociatedParameter"/> would say so authoritatively
/// but is only populated while the contract is being configured, which happens after modifiers run;
/// <see cref="JsonTypeInfo.ConstructorAttributeProvider"/> is available earlier and names the very
/// constructor the serializer picked.
/// </para>
/// <para>
/// Both the reflection-based resolver and the <c>System.Text.Json</c> source generator populate it —
/// the latter through a factory, so reading it here is what forces that factory to run.
/// </para>
/// </remarks>
internal readonly struct ConstructorBinding
{
    private readonly ParameterInfo[]? _parameters;

    private ConstructorBinding(ParameterInfo[]? parameters) => _parameters = parameters;

    public static ConstructorBinding Create(JsonTypeInfo typeInfo) =>
        typeInfo.ConstructorAttributeProvider is MethodBase constructor
            ? new ConstructorBinding(GetParameters(constructor))
            : new ConstructorBinding(null);

    /// <summary>
    /// Finds the constructor parameter a member is bound to. Mirrors the serializer's own matching:
    /// by the CLR member name (the JSON name only when there is none), case-insensitively, with the
    /// parameter type equal to the member type.
    /// </summary>
    /// <remarks>
    /// Matching the JSON name as well would be wrong, not merely generous: a settable member renamed
    /// to a parameter's name would be treated as constructor-bound, and the substituted default would
    /// then be assigned through its setter, overwriting the initializer the condition exists to keep.
    /// </remarks>
    public bool TryGetParameter(
        string? memberName,
        string jsonName,
        Type memberType,
        [NotNullWhen(true)] out ParameterInfo? parameter)
    {
        string lookupName = memberName ?? jsonName;

        if (_parameters is { } parameters)
        {
            foreach (ParameterInfo candidate in parameters)
            {
                if (candidate.ParameterType != memberType || candidate.Name is not { } name)
                {
                    continue;
                }

                if (string.Equals(name, lookupName, StringComparison.OrdinalIgnoreCase))
                {
                    parameter = candidate;
                    return true;
                }
            }
        }

        parameter = null;
        return false;
    }

    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2075:MethodBase.GetParameters",
        Justification = "The MethodBase comes from JsonTypeInfo.ConstructorAttributeProvider; the " +
                        "serializer already keeps that constructor and its parameters alive in order " +
                        "to deserialize through it.")]
    private static ParameterInfo[] GetParameters(MethodBase constructor) => constructor.GetParameters();
}
