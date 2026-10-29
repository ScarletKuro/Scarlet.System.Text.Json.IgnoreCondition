using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Scarlet.System.Text.Json.IgnoreCondition.Internal;

/// <summary>
/// Applies <see cref="JsonIgnoreExAttribute"/> to a resolved contract.
/// </summary>
/// <remarks>
/// Three mechanisms cover the three shapes a member can have, because the serializer reaches each of
/// them differently:
/// <list type="bullet">
/// <item>
/// A settable member whose type already accepts null (a reference type or <see cref="Nullable{T}"/>):
/// the setter is wrapped, so the serializer's own null handling runs untouched and only the assignment
/// is skipped.
/// </item>
/// <item>
/// A settable member of a non-nullable value type: the contract re-declares the member as <c>T?</c>
/// with the same accessors. The serializer then reads the null with its own <c>Nullable&lt;T&gt;</c>
/// converter instead of throwing, and the wrapped setter drops it.
/// </item>
/// <item>
/// A member bound to a deserialization constructor parameter, which never goes through a setter: the
/// converter is intercepted and substitutes the parameter's default value for a null.
/// </item>
/// </list>
/// </remarks>
internal sealed class ExtendedIgnoreConditionModifier
{
    private static readonly HashSet<Type> NumericTypes =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
        typeof(Half), typeof(Int128), typeof(UInt128),
    ];

    // Copied rather than referenced: contracts are resolved lazily and cached, so a settings object
    // mutated afterwards would apply to some types and not others.
    private readonly JsonIgnoreConditionEx? _defaultReadCondition;
    private readonly UnsupportedMemberBehavior _unsupportedMemberBehavior;
    private readonly bool _requireValueTypeRegistration;

    public ExtendedIgnoreConditionModifier(ExtendedIgnoreConditionSettings settings)
    {
        _defaultReadCondition = settings.DefaultReadCondition;
        _unsupportedMemberBehavior = settings.UnsupportedMemberBehavior;
        _requireValueTypeRegistration = settings.RequireValueTypeRegistration;
    }

    public void Modify(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        // Resolved on first use only: a type with no conditions must be left completely untouched, so
        // that it keeps the source-generated serialization fast path.
        ConstructorBinding? constructorBinding = null;

        for (int index = typeInfo.Properties.Count - 1; index >= 0; index--)
        {
            JsonPropertyInfo property = typeInfo.Properties[index];

            // [JsonIgnore] leaves the entry in place with both accessors stripped. The serializer can
            // neither read nor write such a member, so there is nothing for any condition to act on —
            // and refusing here would turn a redundant attribute into a hard failure.
            if (property.Get is null && property.Set is null)
            {
                continue;
            }

            MemberCondition conditions = ReadCondition(property);
            if (conditions.Read is null)
            {
                continue;
            }

            constructorBinding ??= ConstructorBinding.Create(typeInfo);
            ApplyRead(typeInfo, index, property, conditions.Read.Value, conditions.IsExplicit, constructorBinding.Value);
        }
    }

    private MemberCondition ReadCondition(JsonPropertyInfo property)
    {
        JsonIgnoreConditionEx? read = null;
        if (property.AttributeProvider?.GetCustomAttributes(typeof(JsonIgnoreExAttribute), inherit: true)
            is { Length: > 0 } attributes)
            read = ((JsonIgnoreExAttribute)attributes[0]).Condition;

        bool isExplicit = read is not null;
        if (property.AttributeProvider?.GetCustomAttributes(typeof(JsonIgnoreAttribute), inherit: true)
            is { Length: > 0 } nativeAttributes)
        {
            var native = ((JsonIgnoreAttribute)nativeAttributes[0]).Condition;
            if (isExplicit && IgnoreConditionRules.NativeConflictsWithExtended((int)native))
                throw new NotSupportedException(
                    $"'{property.DeclaringType}.{MemberNameOf(property) ?? property.Name}' declares conflicting " +
                    $"read-side ignore conditions: native '{native}' and extended '{read}'.");

            if (!isExplicit && IgnoreConditionRules.NativeSuppressesReadDefault((int)native))
                return default;
        }

        return new MemberCondition(read ?? _defaultReadCondition, isExplicit);
    }

    private void ApplyRead(
        JsonTypeInfo typeInfo,
        int index,
        JsonPropertyInfo property,
        JsonIgnoreConditionEx condition,
        bool isExplicit,
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

        // WhenReadingDefault always needs the typed default comparison; WhenReadingNull only needs
        // typed metadata to reach a constructor parameter or to project a value type as T?.
        bool needsTypedOps = condition == JsonIgnoreConditionEx.WhenReadingDefault ||
                             isConstructorBound ||
                             IsNonNullableValueType(memberType);

        MemberOps? ops = null;
        if (needsTypedOps)
        {
            // The source generator registers value types from a module initializer in the assembly
            // that declares the annotated member. Resolving a contract only reflects over that
            // assembly, which does not run its initializer, so run it before looking the type up.
            RuntimeHelpers.RunModuleConstructor(property.DeclaringType.Module.ModuleHandle);

            ops = MemberOps.Resolve(memberType, _requireValueTypeRegistration);
            if (ops is null)
            {
                // Unlike the other refusals this is a setup mistake, not a member the condition cannot
                // apply to, and it only happens under NativeAOT or RequireValueTypeRegistration.
                // Skipping it silently for an options-level default would make NativeAOT quietly
                // diverge from JIT, so it is reported whether or not the condition is explicit.
                Refuse(isExplicit: true, typeInfo, property, MissingOpsReason(memberType));
                return;
            }

            // Both typed mechanisms wrap the member's converter, so they need the converter itself. The
            // reflection resolver leaves a member-level factory in CustomConverter (the source generator
            // emits the converter it creates), so expand it here as the serializer would.
            if (property.CustomConverter is JsonConverterFactory factory)
            {
                JsonConverter? created = factory.CreateConverter(memberType, typeInfo.Options);
                if (created is null or JsonConverterFactory)
                {
                    Refuse(
                        isExplicit,
                        typeInfo,
                        property,
                        $"its converter factory '{factory.GetType()}' did not create a converter for '{memberType}'.");
                    return;
                }

                property.CustomConverter = created;
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
            Refuse(isExplicit, typeInfo, property, "it has no setter, so there is no assignment to skip.");
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

            // The wrapper discards null before it reaches the model. Let it receive that null even
            // when RespectNullableAnnotations validates the original non-nullable reference member.
            // Keep IsGetNullable unchanged so serialization still enforces the declared annotation.
            if (!memberType.IsValueType)
            {
                property.IsSetNullable = true;
            }
        }
    }

    /// <summary>
    /// Swaps in the <c>T?</c> replacement created by <see cref="MemberOps.CreateNullableProjection"/>,
    /// carrying over everything the original entry configured.
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
        if (projection.ShouldSerialize is null &&
            typeInfo.Options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingDefault)
        {
            // T? has a different default from T: boxed zero/false/default(struct) is non-null.
            // Preserve the original comparison when the options supply the write condition. An
            // existing predicate (including [JsonIgnore(Never)]) already overrides that default.
            projection.ShouldSerialize = (_, value) => !ops.IsDefaultValue(value);
        }
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

    private static string MissingOpsReason(Type memberType)
    {
        Type valueType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        return $"'{valueType}' is a custom value type that has not been registered, and NativeAOT " +
               "cannot create its metadata at run time. Call " +
               $"ExtendedIgnoreConditionResolver.RegisterValueType<{CSharpName(valueType)}>() during startup.";
    }

    private static string CSharpName(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<" +
              $"{string.Join(", ", type.GetGenericArguments().Select(CSharpName))}>"
            : type.Name;

    private static bool IsNonNullableValueType(Type type) =>
        type.IsValueType && Nullable.GetUnderlyingType(type) is null;

    private static bool HasNonDefaultNumberHandling(JsonPropertyInfo property, JsonTypeInfo typeInfo, Type memberType)
    {
        JsonNumberHandling? handling = property.NumberHandling ?? typeInfo.NumberHandling ?? typeInfo.Options.NumberHandling;
        if (handling == JsonNumberHandling.Strict)
        {
            return false;
        }

        Type effective = Nullable.GetUnderlyingType(memberType) ?? memberType;
        return NumericTypes.Contains(effective);
    }

    private void Refuse(bool isExplicit, JsonTypeInfo typeInfo, JsonPropertyInfo property, string reason)
    {
        // A condition that came from an attribute is reported when it cannot be applied; one inherited
        // from an options-level default is best-effort, because it is aimed at every member at once.
        if (!isExplicit || _unsupportedMemberBehavior == UnsupportedMemberBehavior.Skip)
        {
            return;
        }

        throw new NotSupportedException(
            $"The ignore condition on '{typeInfo.Type}.{MemberNameOf(property) ?? property.Name}' " +
            $"cannot be applied because {reason}");
    }

    private static string? MemberNameOf(JsonPropertyInfo property) =>
        (property.AttributeProvider as MemberInfo)?.Name;

    private readonly record struct MemberCondition(JsonIgnoreConditionEx? Read, bool IsExplicit);
}
