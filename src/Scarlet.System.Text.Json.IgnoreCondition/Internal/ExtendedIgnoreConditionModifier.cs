using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Scarlet.Text.Json.Serialization;
using Scarlet.Text.Json.Serialization.Metadata;

namespace Scarlet.Text.Json.Internal;

/// <summary>
/// Applies <see cref="JsonIgnoreExAttribute"/> to a resolved contract.
/// </summary>
/// <remarks>
/// Three mechanisms cover the three shapes a member can have, because the serializer reaches each of
/// them differently:
/// <list type="bullet">
/// <item>
/// A settable member whose type already accepts null (a reference type or
/// <see cref="Nullable{T}"/>): the setter is wrapped, so the serializer's own null handling runs
/// untouched and only the assignment is skipped.
/// </item>
/// <item>
/// A settable member of a non-nullable value type: the contract re-declares the member as
/// <c>T?</c> with the same accessors. The serializer then reads the null with its own
/// <c>Nullable&lt;T&gt;</c> converter instead of throwing, and the wrapped setter drops it.
/// </item>
/// <item>
/// A member bound to a deserialization constructor parameter, which never goes through a setter:
/// the converter is intercepted and substitutes the parameter's default value for a null.
/// </item>
/// </list>
/// </remarks>
internal sealed class ExtendedIgnoreConditionModifier(ExtendedIgnoreConditionSettings settings)
{
    private static readonly HashSet<Type> NumericTypes =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
        typeof(Half), typeof(Int128), typeof(UInt128),
    ];

    public void Modify(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        MemberIgnoreSpec[] registered = CollectRegistered(typeInfo.Type);
        MemberIgnoreSpec[] specs = registered.Length > 0
            ? registered
            : settings.AllowReflectionFallback ? ResolveByReflection(typeInfo) : [];

        if (specs.Length == 0 && !settings.HasDefaults)
        {
            // Leave the contract untouched so that unannotated types keep the source-generated
            // fast path, which any contract customization would disable.
            return;
        }

        ConstructorBinding constructorBinding = ConstructorBinding.Create(typeInfo);
        bool[]? matched = specs.Length > 0 ? new bool[specs.Length] : null;

        for (int index = typeInfo.Properties.Count - 1; index >= 0; index--)
        {
            JsonPropertyInfo property = typeInfo.Properties[index];
            int specIndex = FindSpec(specs, property, typeInfo);
            if (specIndex >= 0 && matched is not null)
            {
                matched[specIndex] = true;
            }

            Apply(typeInfo, index, property, specIndex >= 0 ? specs[specIndex] : null, constructorBinding);
        }

        if (matched is not null)
        {
            VerifyDeclaredSpecsMatched(typeInfo, specs, matched);
        }
    }

    private void Apply(
        JsonTypeInfo typeInfo,
        int index,
        JsonPropertyInfo property,
        MemberIgnoreSpec? spec,
        ConstructorBinding constructorBinding)
    {
        JsonIgnoreConditionEx? read = spec?.ReadCondition
            ?? (spec?.SuppressReadDefault == true ? null : settings.DefaultReadCondition);
        JsonIgnoreConditionEx? write = spec?.WriteCondition
            ?? (spec?.SuppressWriteDefault == true ? null : settings.DefaultWriteCondition);

        // A condition that came from an attribute is reported when it cannot be applied; one inherited
        // from the options-level default is best-effort, because it is aimed at every member at once.
        bool readIsExplicit = spec?.ReadCondition is not null;
        bool writeIsExplicit = spec?.WriteCondition is not null;

        if (spec?.IgnoreAlways == true)
        {
            if (constructorBinding.TryGetParameter(MemberNameOf(property), property.Name, property.PropertyType, out _))
            {
                Refuse(
                    isExplicit: true,
                    typeInfo,
                    property,
                    "it is bound to a deserialization constructor parameter, which the serializer " +
                    "always has to supply a value for. Remove the parameter or use WhenWriting.");
                return;
            }

            typeInfo.Properties.RemoveAt(index);
            return;
        }

        if (write is { } writeCondition && writeCondition != JsonIgnoreConditionEx.Never)
        {
            ApplyWrite(typeInfo, property, writeCondition, writeIsExplicit, spec);
        }

        if (read is { } readCondition && readCondition != JsonIgnoreConditionEx.Never)
        {
            ApplyRead(typeInfo, index, property, readCondition, readIsExplicit, spec, constructorBinding);
        }
    }

    private void ApplyWrite(
        JsonTypeInfo typeInfo,
        JsonPropertyInfo property,
        JsonIgnoreConditionEx condition,
        bool isExplicit,
        MemberIgnoreSpec? spec)
    {
        switch (condition)
        {
            case JsonIgnoreConditionEx.WhenWriting:
                property.ShouldSerialize = static (_, _) => false;
                break;

            case JsonIgnoreConditionEx.WhenWritingNull:
                property.ShouldSerialize = static (_, value) => value is not null;
                break;

            case JsonIgnoreConditionEx.WhenWritingDefault:
                MemberOps? ops = spec?.Ops ?? TryCreateOps(property.PropertyType);
                if (ops is null)
                {
                    Refuse(isExplicit, typeInfo, property, MissingOpsReason(property.PropertyType));
                    return;
                }

                property.ShouldSerialize = (_, value) => !ops.IsDefaultValue(value);
                break;
        }
    }

    private void ApplyRead(
        JsonTypeInfo typeInfo,
        int index,
        JsonPropertyInfo property,
        JsonIgnoreConditionEx condition,
        bool isExplicit,
        MemberIgnoreSpec? spec,
        ConstructorBinding constructorBinding)
    {
        if (property.IsExtensionData)
        {
            Refuse(isExplicit, typeInfo, property, "it is the extension data property.");
            return;
        }

        if (property.IsRequired)
        {
            Refuse(
                isExplicit,
                typeInfo,
                property,
                "it is required, so the serializer must observe a value for it. Drop 'required' or " +
                "the condition.");
            return;
        }

        Type memberType = property.PropertyType;
        bool isConstructorBound = constructorBinding.TryGetParameter(
            MemberNameOf(property),
            property.Name,
            memberType,
            out ParameterInfo? parameter);

        if (condition == JsonIgnoreConditionEx.WhenReading)
        {
            if (isConstructorBound)
            {
                Refuse(
                    isExplicit,
                    typeInfo,
                    property,
                    "it is bound to a deserialization constructor parameter, which the serializer " +
                    "always has to supply a value for.");
                return;
            }

            // A property with no setter is skipped over by the serializer.
            property.Set = null;
            return;
        }

        // WhenReadingDefault always needs the typed default comparison; WhenReadingNull only needs
        // typed metadata to reach a constructor parameter or to project a value type as T?.
        bool needsTypedOps = condition == JsonIgnoreConditionEx.WhenReadingDefault ||
                             isConstructorBound ||
                             IsNonNullableValueType(memberType);

        MemberOps? ops = null;
        if (needsTypedOps)
        {
            ops = spec?.Ops ?? TryCreateOps(memberType);
            if (ops is null)
            {
                Refuse(isExplicit, typeInfo, property, MissingOpsReason(memberType));
                return;
            }
        }

        if (isConstructorBound)
        {
            if (!ConverterBasedTypes.IsConverterBased(memberType, property.CustomConverter is not null))
            {
                Refuse(
                    isExplicit,
                    typeInfo,
                    property,
                    $"'{memberType}' is not serialized by a single-token converter, and intercepting " +
                    "a constructor parameter of such a type would lose reference handling, " +
                    "polymorphic metadata and resumable reads.");
                return;
            }

            if (HasNonDefaultNumberHandling(property, typeInfo, memberType))
            {
                Refuse(
                    isExplicit,
                    typeInfo,
                    property,
                    "intercepting the converter of a numeric constructor parameter would drop the " +
                    "JsonNumberHandling configured for it.");
                return;
            }

            object? nullReplacement = parameter!.HasDefaultValue ? parameter.DefaultValue : null;
            property.CustomConverter = ops!.CreateDefaultOnNullConverter(
                property.CustomConverter,
                nullReplacement,
                replaceTypeDefaults: condition == JsonIgnoreConditionEx.WhenReadingDefault);
            return;
        }

        if (property.Set is null)
        {
            Refuse(
                isExplicit,
                typeInfo,
                property,
                "it has no setter, so there is no assignment to skip.");
            return;
        }

        if (ops?.SupportsNullableProjection == true)
        {
            if (ops.CreateNullableProjection(typeInfo, property) is not { } projection)
            {
                Refuse(
                    isExplicit,
                    typeInfo,
                    property,
                    $"the converter declared on it cannot be lifted over 'System.Nullable<{memberType}>'.");
                return;
            }

            CompleteNullableProjection(typeInfo, index, property, projection, ops, condition);
        }
        else
        {
            property.Set = CreateSkippingSetter(property.Set!, ops, condition);
        }
    }

    /// <summary>
    /// Swaps in the <c>T?</c> replacement created by <see cref="MemberOps.CreateNullableProjection"/>,
    /// carrying over everything the original entry configured. The serializer then reads an incoming
    /// null with its own <c>Nullable&lt;T&gt;</c> converter instead of throwing, and the wrapped setter
    /// drops it.
    /// </summary>
    private static void CompleteNullableProjection(
        JsonTypeInfo typeInfo,
        int index,
        JsonPropertyInfo property,
        JsonPropertyInfo projection,
        MemberOps ops,
        JsonIgnoreConditionEx condition)
    {
        projection.Get = property.Get;
        projection.Set = CreateSkippingSetter(property.Set!, ops, condition);
        projection.Order = property.Order;
        projection.ObjectCreationHandling = property.ObjectCreationHandling;
        projection.ShouldSerialize = property.ShouldSerialize;
        projection.AttributeProvider = property.AttributeProvider;
        projection.IsGetNullable = true;
        projection.IsSetNullable = true;

        typeInfo.Properties[index] = projection;
    }

    private static Action<object, object?> CreateSkippingSetter(
        Action<object, object?> inner,
        MemberOps? ops,
        JsonIgnoreConditionEx condition)
    {
        if (condition == JsonIgnoreConditionEx.WhenReadingDefault)
        {
            MemberOps typedOps = ops!;
            return (target, value) =>
            {
                if (!typedOps.IsDefaultValue(value))
                {
                    inner(target, value);
                }
            };
        }

        return (target, value) =>
        {
            if (value is not null)
            {
                inner(target, value);
            }
        };
    }

    private static string MissingOpsReason(Type memberType) =>
        $"'{memberType}' needs metadata that can only be produced at run time on this platform. " +
        "Annotate the member with [JsonIgnoreEx] so the source generator emits it at compile time.";

    private static bool IsNonNullableValueType(Type type) =>
        type.IsValueType && Nullable.GetUnderlyingType(type) is null;

    private static bool HasNonDefaultNumberHandling(JsonPropertyInfo property, JsonTypeInfo typeInfo, Type memberType)
    {
        JsonNumberHandling? handling = property.NumberHandling ?? typeInfo.NumberHandling ?? typeInfo.Options.NumberHandling;
        if (handling is null or JsonNumberHandling.Strict)
        {
            return false;
        }

        Type effective = Nullable.GetUnderlyingType(memberType) ?? memberType;
        return NumericTypes.Contains(effective);
    }

    private void Refuse(bool isExplicit, JsonTypeInfo typeInfo, JsonPropertyInfo property, string reason)
    {
        if (!isExplicit || settings.UnsupportedMemberBehavior == UnsupportedMemberBehavior.Skip)
        {
            return;
        }

        throw new NotSupportedException(
            $"The ignore condition on '{typeInfo.Type}.{MemberNameOf(property) ?? property.Name}' " +
            $"cannot be applied because {reason}");
    }

    private void VerifyDeclaredSpecsMatched(JsonTypeInfo typeInfo, MemberIgnoreSpec[] specs, bool[] matched)
    {
        if (settings.UnsupportedMemberBehavior == UnsupportedMemberBehavior.Skip)
        {
            return;
        }

        for (int i = 0; i < specs.Length; i++)
        {
            MemberIgnoreSpec spec = specs[i];
            bool declaresCondition = spec.IgnoreAlways || spec.ReadCondition is not null || spec.WriteCondition is not null;

            // Only entries registered for this exact type are verified: an inherited entry may
            // legitimately not surface on a derived contract, and reflection-derived entries are
            // built from the contract itself and so always match.
            if (matched[i] || !declaresCondition || spec.DeclaringType != typeInfo.Type)
            {
                continue;
            }

            throw new NotSupportedException(
                $"Member '{typeInfo.Type}.{spec.MemberName}' declares an ignore condition but the " +
                "serializer did not surface a matching JSON property of type " +
                $"'{spec.Ops.MemberType}'. It may be ignored by [JsonIgnore], be non-public without " +
                "[JsonInclude], or have been renamed.");
        }
    }

    private static string? MemberNameOf(JsonPropertyInfo property) =>
        (property.AttributeProvider as MemberInfo)?.Name;

    private static int FindSpec(MemberIgnoreSpec[] specs, JsonPropertyInfo property, JsonTypeInfo typeInfo)
    {
        if (specs.Length == 0)
        {
            return -1;
        }

        string? memberName = MemberNameOf(property);

        // Exact matches first: an entry keyed by a constructor parameter name may differ from the
        // member name only in case, and must not win over an exactly matching member.
        for (int pass = 0; pass < 2; pass++)
        {
            StringComparison comparison = pass == 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            for (int i = 0; i < specs.Length; i++)
            {
                MemberIgnoreSpec spec = specs[i];
                if (spec.Ops.MemberType != property.PropertyType)
                {
                    continue;
                }

                if (memberName is not null)
                {
                    if (string.Equals(spec.MemberName, memberName, comparison))
                    {
                        return i;
                    }

                    continue;
                }

                if (MatchesJsonName(spec.MemberName, property.Name, typeInfo.Options))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Fallback used when the contract carries no attribute provider to read a CLR member name from:
    /// compare against the JSON name, both verbatim and as the naming policy would have written it.
    /// </summary>
    private static bool MatchesJsonName(string memberName, string jsonName, JsonSerializerOptions options)
    {
        StringComparison comparison = options.PropertyNameCaseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (string.Equals(jsonName, memberName, comparison))
        {
            return true;
        }

        return options.PropertyNamingPolicy is { } policy &&
               string.Equals(jsonName, policy.ConvertName(memberName), comparison);
    }

    /// <summary>
    /// Gathers registrations for the type and everything it inherits from, so that an annotated base
    /// class keeps working through a derived contract. Derived registrations come first and therefore
    /// win when a member is shadowed.
    /// </summary>
    private static MemberIgnoreSpec[] CollectRegistered(Type type)
    {
        MemberIgnoreSpec[]? own = ExtendedIgnoreConditionRegistry.TryGetSpecs(type);
        List<MemberIgnoreSpec>? merged = null;

        for (Type? current = type.BaseType; current is not null && current != typeof(object); current = current.BaseType)
        {
            if (ExtendedIgnoreConditionRegistry.TryGetSpecs(current) is { Length: > 0 } inherited)
            {
                merged ??= own is { Length: > 0 } ? [.. own] : [];
                merged.AddRange(inherited);
            }
        }

        return merged?.ToArray() ?? own ?? [];
    }

    [UnconditionalSuppressMessage(
        "TrimAnalysis",
        "IL2026:RequiresUnreferencedCode",
        Justification = "See the IL3050 justification on the same member.")]
    [UnconditionalSuppressMessage(
        "AotAnalysis",
        "IL3050:RequiresDynamicCode",
        Justification = "Guarded by RuntimeFeature.IsDynamicCodeSupported. Reached only when a " +
                        "condition comes from an options-level default or the reflection fallback; " +
                        "the source generator supplies this metadata at compile time.")]
    private static MemberOps? TryCreateOps(Type memberType) =>
        RuntimeFeature.IsDynamicCodeSupported ? MemberOps.Create(memberType) : null;

    [UnconditionalSuppressMessage(
        "TrimAnalysis",
        "IL2026:RequiresUnreferencedCode",
        Justification = "See the IL3050 justification on the same member.")]
    [UnconditionalSuppressMessage(
        "AotAnalysis",
        "IL3050:RequiresDynamicCode",
        Justification = "Guarded by ExtendedIgnoreConditionSettings.AllowReflectionFallback and by " +
                        "RuntimeFeature.IsDynamicCodeSupported; NativeAOT consumers use the source " +
                        "generator, which registers this metadata at compile time.")]
    private static MemberIgnoreSpec[] ResolveByReflection(JsonTypeInfo typeInfo) =>
        RuntimeFeature.IsDynamicCodeSupported ? ReflectionSpecProvider.GetSpecs(typeInfo) : [];
}
