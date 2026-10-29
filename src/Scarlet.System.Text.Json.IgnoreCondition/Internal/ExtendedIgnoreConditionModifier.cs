using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

        // A member without its own converter uses one from the options or from its type. If that one
        // reads JSON null itself, it would turn the null into a value before the condition sees it.
        JsonConverter? typeLevelConverter = property.CustomConverter is null
            ? FindTypeLevelConverter(memberType, typeInfo.Options)
            : null;
        bool typeLevelHandlesNull = ConverterHandlesNull(typeLevelConverter);

        // WhenReadingDefault always needs the typed default comparison; WhenReadingNull only needs
        // typed metadata to reach a constructor parameter, to project a value type as T?, or to route
        // a null past a converter that handles it (see the setter path below).
        bool needsTypedOps = condition == JsonIgnoreConditionEx.WhenReadingDefault ||
                             isConstructorBound ||
                             IsNonNullableValueType(memberType) ||
                             property.CustomConverter is not null ||
                             typeLevelHandlesNull;

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

            // The serializer also accepts a member converter for a base type, such as a
            // JsonConverter<object> on an int, and adapts it with an internal casting converter after
            // modifiers have run. Wrapping needs a converter for the exact member type, so refuse any
            // other rather than fail later with an InvalidCastException.
            bool wrapsConverter = isConstructorBound ||
                                  ops.SupportsNullableProjection ||
                                  ConverterHandlesNull(property.CustomConverter);
            if (wrapsConverter &&
                property.CustomConverter is { } memberConverter &&
                memberConverter.Type != memberType)
            {
                Refuse(
                    isExplicit,
                    typeInfo,
                    property,
                    $"its converter '{memberConverter.GetType()}' converts '{memberConverter.Type}', not " +
                    $"'{memberType}', and only a converter for the member's own type can be wrapped.");
                return;
            }

            // A value type re-declared as T? gets the serializer's Nullable<T> converter around the
            // converter for T, which passes a null on to that converter if it handles null. Lift it
            // explicitly instead, the way a member converter is, so the null stays a null.
            if (typeLevelHandlesNull && ops.SupportsNullableProjection)
            {
                if (typeLevelConverter!.Type != memberType)
                {
                    Refuse(
                        isExplicit,
                        typeInfo,
                        property,
                        $"the converter '{typeLevelConverter.GetType()}' in effect for '{memberType}' reads " +
                        $"JSON null itself but converts '{typeLevelConverter.Type}', so the null cannot be " +
                        "kept from it.");
                    return;
                }

                property.CustomConverter = typeLevelConverter;
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

            object? nullReplacement = parameter!.HasDefaultValue ? NormalizeDefault(parameter.DefaultValue, memberType) : null;
            property.CustomConverter = ops!.CreateDefaultOnNullConverter(
                property.CustomConverter,
                nullReplacement,
                replaceTypeDefaults: condition == JsonIgnoreConditionEx.WhenReadingDefault);
            return;
        }

        if (IsAssignedThroughObjectInitializer(typeInfo, property))
        {
            Refuse(
                isExplicit,
                typeInfo,
                property,
                "it is init-only, and the System.Text.Json source generator assigns init-only members " +
                "through an object initializer: the setter the condition relies on is never called, and " +
                "a member absent from the JSON loses its initializer too. Use a set accessor, a " +
                "constructor parameter, or the reflection-based resolver.");
            return;
        }

        if (property.Set is null)
        {
            Refuse(isExplicit, typeInfo, property, "it has no setter, so there is no assignment to skip.");
            return;
        }

        if (ops?.SupportsNullableProjection == true)
        {
            CompleteNullableProjection(typeInfo, index, property, ops.CreateNullableProjection(typeInfo, property), ops, condition);
        }
        else
        {
            // Every other path skips a JSON null before any converter sees it. A converter that handles
            // null itself, the member's own or the one in effect for its type, would turn it into a
            // value first, which the skipping setter would then assign, so route the null past it. The
            // converter still reads every other token and writes; without a member converter, the
            // wrapper resolves the one in effect at run time.
            if (ConverterHandlesNull(property.CustomConverter) || typeLevelHandlesNull)
            {
                property.CustomConverter = ops!.CreateDefaultOnNullConverter(
                    property.CustomConverter,
                    nullReplacement: null,
                    replaceTypeDefaults: false);
            }

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
        return $"'{valueType}' is a value type that has not been registered, and NativeAOT " +
               "cannot create its metadata at run time. Call " +
               $"ExtendedIgnoreConditionResolver.RegisterValueType<{CSharpName(valueType)}>() during startup.";
    }

    private static string CSharpName(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<" +
              $"{string.Join(", ", type.GetGenericArguments().Select(CSharpName))}>"
            : type.Name;

    /// <summary>
    /// The converter the serializer uses for a member without its own, if the user supplied one: the first
    /// options converter that accepts the type, else a <see cref="JsonConverterAttribute"/> on the type.
    /// For <c>T?</c> the converter for <c>T</c> counts too, since the serializer builds <c>T?</c> on it.
    /// Built-in converters are not considered; the serializer reads null for them as documented.
    /// </summary>
    private static JsonConverter? FindTypeLevelConverter(Type memberType, JsonSerializerOptions options) =>
        FindUserConverter(memberType, options) ??
        (Nullable.GetUnderlyingType(memberType) is { } underlying ? FindUserConverter(underlying, options) : null);

    private static JsonConverter? FindUserConverter(Type type, JsonSerializerOptions options)
    {
        foreach (JsonConverter converter in options.Converters)
        {
            if (converter.CanConvert(type))
            {
                return Expand(converter, type, options);
            }
        }

        if (type.GetCustomAttribute<JsonConverterAttribute>(inherit: false) is { } attribute)
        {
            JsonConverter? converter = attribute.CreateConverter(type) ??
                (attribute.ConverterType is { } converterType ? (JsonConverter?)Activator.CreateInstance(converterType) : null);
            return Expand(converter, type, options);
        }

        return null;
    }

    private static JsonConverter? Expand(JsonConverter? converter, Type type, JsonSerializerOptions options) =>
        converter is JsonConverterFactory factory ? factory.CreateConverter(type, options) : converter;

    /// <summary>
    /// Whether a converter reads JSON null itself. Decided by the type it converts, which for a converter
    /// of a base type is not the member type.
    /// </summary>
    private static bool ConverterHandlesNull(JsonConverter? converter) =>
        converter?.Type is { } converted &&
        MemberOps.Resolve(converted, requireRegistration: false)?.HandlesNull(converter) == true;

    /// <summary>
    /// Reflection reports an enum parameter's default value as its underlying integer. That unboxes to the
    /// enum itself, but not to <c>Nullable&lt;TEnum&gt;</c>, so convert it to the enum first.
    /// </summary>
    private static object? NormalizeDefault(object? value, Type memberType) =>
        value is not null &&
        (Nullable.GetUnderlyingType(memberType) ?? memberType) is { IsEnum: true } enumType &&
        value.GetType() != enumType
            ? Enum.ToObject(enumType, value)
            : value;

    /// <summary>
    /// Whether the serializer assigns the member through an object initializer instead of its setter. The
    /// System.Text.Json source generator does that for init-only members, passing each one like a
    /// constructor argument; the reflection-based resolver calls the init accessor like any setter.
    /// </summary>
    private static bool IsAssignedThroughObjectInitializer(JsonTypeInfo typeInfo, JsonPropertyInfo property) =>
        typeInfo.OriginatingResolver is JsonSerializerContext &&
        property.AttributeProvider is PropertyInfo { SetMethod: { } setter } &&
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

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
