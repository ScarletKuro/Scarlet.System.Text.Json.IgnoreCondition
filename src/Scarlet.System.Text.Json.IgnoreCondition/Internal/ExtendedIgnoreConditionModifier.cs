using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Scarlet.Text.Json.Serialization;

namespace Scarlet.Text.Json.Internal;

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

            MemberConditions conditions = ReadConditions(property);
            if (conditions.IsEmpty)
            {
                continue;
            }

            constructorBinding ??= ConstructorBinding.Create(typeInfo);
            Apply(typeInfo, index, property, conditions, constructorBinding.Value);
        }
    }

    /// <summary>
    /// Folds the member's attributes, and any options-level default, into one read-side and one
    /// write-side condition.
    /// </summary>
    private MemberConditions ReadConditions(JsonPropertyInfo property)
    {
        JsonIgnoreConditionEx? read = null;
        JsonIgnoreConditionEx? write = null;
        bool always = false;
        bool suppressDefaults = false;

        if (property.AttributeProvider?.GetCustomAttributes(typeof(JsonIgnoreExAttribute), inherit: true)
            is { Length: > 0 } attributes)
        {
            foreach (object attribute in attributes)
            {
                JsonIgnoreConditionEx condition = ((JsonIgnoreExAttribute)attribute).Condition;

                if (condition == JsonIgnoreConditionEx.Always)
                {
                    always = true;
                }
                else if (condition == JsonIgnoreConditionEx.Never)
                {
                    suppressDefaults = true;
                }
                else if (ConditionKinds.IsReadSide(condition))
                {
                    ThrowIfConflicting(property, read, condition, "read");
                    read = condition;
                }
                else
                {
                    ThrowIfConflicting(property, write, condition, "write");
                    write = condition;
                }
            }
        }

        bool readIsExplicit = read is not null;
        bool writeIsExplicit = write is not null;

        if (!suppressDefaults)
        {
            read ??= settings.DefaultReadCondition;
            write ??= settings.DefaultWriteCondition;
        }

        return new MemberConditions(
            Read: read == JsonIgnoreConditionEx.Never ? null : read,
            Write: write == JsonIgnoreConditionEx.Never ? null : write,
            Always: always,
            ReadIsExplicit: readIsExplicit,
            WriteIsExplicit: writeIsExplicit);
    }

    private static void ThrowIfConflicting(
        JsonPropertyInfo property,
        JsonIgnoreConditionEx? existing,
        JsonIgnoreConditionEx incoming,
        string side)
    {
        if (existing is { } already && already != incoming)
        {
            throw new NotSupportedException(
                $"'{property.DeclaringType}.{MemberNameOf(property) ?? property.Name}' declares two " +
                $"conflicting {side}-side ignore conditions: '{already}' and '{incoming}'.");
        }
    }

    private void Apply(
        JsonTypeInfo typeInfo,
        int index,
        JsonPropertyInfo property,
        MemberConditions conditions,
        ConstructorBinding constructorBinding)
    {
        if (conditions.Always)
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

        if (conditions.Write is { } writeCondition)
        {
            ApplyWrite(typeInfo, property, writeCondition, conditions.WriteIsExplicit);
        }

        if (conditions.Read is { } readCondition)
        {
            ApplyRead(typeInfo, index, property, readCondition, conditions.ReadIsExplicit, constructorBinding);
        }
    }

    private void ApplyWrite(
        JsonTypeInfo typeInfo,
        JsonPropertyInfo property,
        JsonIgnoreConditionEx condition,
        bool isExplicit)
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
                if (MemberOps.Resolve(property.PropertyType) is not { } ops)
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
            ops = MemberOps.Resolve(memberType);
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
        $"'{memberType}' is a value type whose metadata this platform cannot produce at run time. " +
        $"Call ExtendedIgnoreConditionResolver.RegisterValueType<{memberType.Name}>() during startup " +
        "to make it available.";

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
        if (!isExplicit || settings.UnsupportedMemberBehavior == UnsupportedMemberBehavior.Skip)
        {
            return;
        }

        throw new NotSupportedException(
            $"The ignore condition on '{typeInfo.Type}.{MemberNameOf(property) ?? property.Name}' " +
            $"cannot be applied because {reason}");
    }

    private static string? MemberNameOf(JsonPropertyInfo property) =>
        (property.AttributeProvider as MemberInfo)?.Name;

    private readonly record struct MemberConditions(
        JsonIgnoreConditionEx? Read,
        JsonIgnoreConditionEx? Write,
        bool Always,
        bool ReadIsExplicit,
        bool WriteIsExplicit)
    {
        public bool IsEmpty => !Always && Read is null && Write is null;
    }
}
