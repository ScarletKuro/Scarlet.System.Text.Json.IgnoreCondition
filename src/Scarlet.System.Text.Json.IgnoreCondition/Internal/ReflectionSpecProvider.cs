using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Scarlet.Text.Json.Serialization;

namespace Scarlet.Text.Json.Internal;

/// <summary>
/// Discovers <see cref="JsonIgnoreExAttribute"/> by reading the attributes of the members the
/// serializer has already surfaced, for assemblies the Scarlet source generator did not process.
/// </summary>
/// <remarks>
/// This is the fallback path. It is neither trim- nor NativeAOT-clean, because building the typed
/// <see cref="MemberOps"/> for a member type only known at run time needs a generic instantiation.
/// The generator exists precisely so that this path is not needed; it is gated on
/// <see cref="ExtendedIgnoreConditionSettings.AllowReflectionFallback"/>.
/// </remarks>
[RequiresUnreferencedCode("Reads [JsonIgnoreEx] by reflection and builds MemberOps<T> for member types only known at run time.")]
[RequiresDynamicCode("Builds MemberOps<T> for member types only known at run time.")]
internal static class ReflectionSpecProvider
{
    public static MemberIgnoreSpec[] GetSpecs(JsonTypeInfo typeInfo)
    {
        IgnoreConditionBuilder? builder = null;

        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            if (property.AttributeProvider is not MemberInfo member)
            {
                continue;
            }

            object[] attributes = GetAttributes(member);
            if (attributes.Length == 0)
            {
                continue;
            }

            builder ??= new IgnoreConditionBuilder();
            MemberOps ops = MemberOps.Create(property.PropertyType);

            foreach (object attribute in attributes)
            {
                builder.Add(member.Name, ((JsonIgnoreExAttribute)attribute).Condition, ops);
            }
        }

        AddConstructorParameterSpecs(typeInfo, ref builder);

        return builder?.Build() ?? [];
    }

    /// <summary>
    /// Picks up <c>[JsonIgnoreEx]</c> written directly on a positional record parameter, where the
    /// compiler puts the attribute on the parameter rather than on the generated property.
    /// </summary>
    private static void AddConstructorParameterSpecs(JsonTypeInfo typeInfo, ref IgnoreConditionBuilder? builder)
    {
        if (typeInfo.ConstructorAttributeProvider is not MethodBase constructor)
        {
            return;
        }

        foreach (ParameterInfo parameter in GetParameters(constructor))
        {
            if (parameter.Name is not { } name)
            {
                continue;
            }

            object[] attributes = GetAttributes(parameter);
            if (attributes.Length == 0)
            {
                continue;
            }

            builder ??= new IgnoreConditionBuilder();
            MemberOps ops = MemberOps.Create(parameter.ParameterType);

            foreach (object attribute in attributes)
            {
                builder.Add(name, ((JsonIgnoreExAttribute)attribute).Condition, ops);
            }
        }
    }

    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2075:MethodBase.GetParameters",
        Justification = "The MethodBase comes from JsonTypeInfo.ConstructorAttributeProvider; the " +
                        "serializer already keeps that constructor and its parameters alive.")]
    private static ParameterInfo[] GetParameters(MethodBase constructor) => constructor.GetParameters();

    private static object[] GetAttributes(ParameterInfo parameter) =>
        parameter.GetCustomAttributes(typeof(JsonIgnoreExAttribute), inherit: true);

    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2075:MemberInfo.GetCustomAttributes",
        Justification = "The MemberInfo comes from JsonPropertyInfo.AttributeProvider, so the member " +
                        "is already rooted by the serializer. Custom attributes on a rooted member " +
                        "survive trimming unless attributes are explicitly stripped.")]
    private static object[] GetAttributes(MemberInfo member) =>
        member.GetCustomAttributes(typeof(JsonIgnoreExAttribute), inherit: true);
}
