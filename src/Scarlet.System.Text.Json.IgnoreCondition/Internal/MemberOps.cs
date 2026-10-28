using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Scarlet.System.Text.Json.IgnoreCondition.Internal.Converters;

namespace Scarlet.System.Text.Json.IgnoreCondition.Internal;

/// <summary>
/// The operations on a member that need the member type as a generic argument rather than as a
/// <see cref="Type"/> value.
/// </summary>
/// <remarks>
/// <para>
/// Only two of the three mechanisms need this: projecting a non-nullable value type as <c>T?</c>, and
/// substituting a constructor parameter's default. Wrapping a setter is done through the non-generic
/// <c>Action&lt;object, object?&gt;</c> and needs nothing here.
/// </para>
/// <para>
/// <see cref="Resolve"/> answers from a table of statically instantiated entries first, so the common
/// value types cost no runtime generic instantiation and stay usable under NativeAOT. Reference types
/// fall back to <see cref="Type.MakeGenericType"/>, which is safe there because the runtime shares one
/// canonical instantiation across all reference types. A value type that is neither in the table nor
/// registered by the consumer is the one case that cannot be served without dynamic code.
/// </para>
/// </remarks>
internal abstract class MemberOps
{
    // Rooted or registered, and therefore available everywhere. Kept apart from instances created on
    // demand, so that RequireValueTypeRegistration is not satisfied by an earlier JIT-only creation.
    private static readonly ConcurrentDictionary<Type, MemberOps> Known = new();
    private static readonly ConcurrentDictionary<Type, MemberOps> Created = new();

    static MemberOps()
    {
        // Rooting these instantiations is what makes the table AOT-safe: the generic code behind each
        // one is emitted at build time rather than being asked for at run time.
        Register<bool>();
        Register<byte>();
        Register<sbyte>();
        Register<char>();
        Register<short>();
        Register<ushort>();
        Register<int>();
        Register<uint>();
        Register<long>();
        Register<ulong>();
        Register<float>();
        Register<double>();
        Register<decimal>();
        Register<Half>();
        Register<Int128>();
        Register<UInt128>();
        Register<Guid>();
        Register<DateTime>();
        Register<DateTimeOffset>();
        Register<DateOnly>();
        Register<TimeOnly>();
        Register<TimeSpan>();

        // Roots the shared reference-type instantiation, so MakeGenericType over any reference type
        // has canonical code to reuse.
        Known[typeof(string)] = MemberOps<string>.Instance;
        Known[typeof(object)] = MemberOps<object>.Instance;
    }

    public abstract Type MemberType { get; }

    /// <summary>
    /// Whether the member type is a non-nullable value type, and can therefore be re-declared in the
    /// contract as <c>T?</c>.
    /// </summary>
    public virtual bool SupportsNullableProjection => false;

    /// <summary>Whether a boxed value equals the default value of the member type.</summary>
    public abstract bool IsDefaultValue(object? boxedValue);

    public abstract JsonConverter CreateDefaultOnNullConverter(
        JsonConverter? inner,
        object? nullReplacement,
        bool replaceTypeDefaults);

    /// <summary>
    /// Creates a replacement contract entry of type <c>T?</c> for the same JSON name, or
    /// <see langword="null"/> when the member declares a converter that cannot be lifted over
    /// <see cref="Nullable{T}"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately built through <see cref="JsonMetadataServices"/> rather than
    /// <see cref="JsonTypeInfo.CreateJsonPropertyInfo"/>: the latter instantiates
    /// <c>JsonPropertyInfo&lt;T?&gt;</c> reflectively, which a NativeAOT build has no guarantee of
    /// having pre-generated.
    /// </remarks>
    public virtual JsonPropertyInfo? CreateNullableProjection(JsonTypeInfo declaringTypeInfo, JsonPropertyInfo source) =>
        throw new InvalidOperationException($"'{MemberType}' is not a non-nullable value type.");

    /// <summary>
    /// Adds <typeparamref name="T"/> and <c>T?</c> to the table, rooting their generic code.
    /// </summary>
    public static void Register<T>()
        where T : struct
    {
        Known[typeof(T)] = ValueMemberOps<T>.Instance;
        Known[typeof(T?)] = MemberOps<T?>.Instance;
    }

    /// <summary>
    /// Returns the operations for a member type, or <see langword="null"/> when they would require
    /// runtime generic instantiation that this platform cannot provide, or that
    /// <paramref name="requireRegistration"/> forbids.
    /// </summary>
    public static MemberOps? Resolve(Type memberType, bool requireRegistration)
    {
        if (Known.TryGetValue(memberType, out MemberOps? ops))
        {
            return ops;
        }

        // Reference types share one canonical instantiation, which the static constructor rooted;
        // value types each need their own, which only a JIT can produce on demand.
        if (memberType.IsValueType && (requireRegistration || !RuntimeFeature.IsDynamicCodeSupported))
        {
            return null;
        }

        return Created.GetOrAdd(memberType, CreateUnchecked);
    }

    [UnconditionalSuppressMessage(
        "AotAnalysis",
        "IL3050:RequiresDynamicCode",
        Justification = "Resolve only reaches this for a reference type, whose canonical instantiation " +
                        "is rooted by the static constructor, or when RuntimeFeature." +
                        "IsDynamicCodeSupported reports that the instantiation can be produced.")]
    [UnconditionalSuppressMessage(
        "TrimAnalysis",
        "IL2055:MakeGenericType",
        Justification = "Both candidate types are referenced directly here, so the trimmer keeps them.")]
    [UnconditionalSuppressMessage(
        "TrimAnalysis",
        "IL2071:MakeGenericType",
        Justification = "MemberOps<T> and ValueMemberOps<T> are referenced directly here, so their " +
                        "parameterless constructors are kept.")]
    private static MemberOps CreateUnchecked(Type memberType)
    {
        bool isNonNullableValueType = memberType.IsValueType && Nullable.GetUnderlyingType(memberType) is null;
        Type openOpsType = isNonNullableValueType ? typeof(ValueMemberOps<>) : typeof(MemberOps<>);
        return (MemberOps)Activator.CreateInstance(openOpsType.MakeGenericType(memberType))!;
    }
}

internal class MemberOps<T> : MemberOps
{
    public static readonly MemberOps<T> Instance = new();

    public override Type MemberType => typeof(T);

    public override bool IsDefaultValue(object? boxedValue) =>
        boxedValue is null || EqualityComparer<T>.Default.Equals((T)boxedValue, default!);

    public override JsonConverter CreateDefaultOnNullConverter(
        JsonConverter? inner,
        object? nullReplacement,
        bool replaceTypeDefaults) =>
        new DefaultOnNullConverter<T>(
            (JsonConverter<T>?)inner,
            nullReplacement is null ? default : (T)nullReplacement,
            replaceTypeDefaults);
}

internal sealed class ValueMemberOps<T> : MemberOps<T>
    where T : struct
{
    public new static readonly ValueMemberOps<T> Instance = new();

    public override bool SupportsNullableProjection => true;

    public override JsonPropertyInfo? CreateNullableProjection(JsonTypeInfo declaringTypeInfo, JsonPropertyInfo source)
    {
        JsonSerializerOptions options = declaringTypeInfo.Options;
        JsonConverter<T?> converter;

        if (source.CustomConverter is { } memberConverter)
        {
            if (memberConverter is not JsonConverter<T> typedConverter)
            {
                // A factory or otherwise untyped converter cannot be lifted; refuse rather than
                // silently drop the member's converter.
                return null;
            }

            converter = new NullableWrapConverter<T>(typedConverter);
        }
        else
        {
            // The serializer's own Nullable<T> converter, so a complex struct keeps full fidelity
            // (reference handling, resumable reads) rather than being read through a shim.
            converter = JsonMetadataServices.GetNullableConverter<T>(options);
        }

        return JsonMetadataServices.CreatePropertyInfo(
            options,
            new JsonPropertyInfoValues<T?>
            {
                IsProperty = source.AttributeProvider is not FieldInfo,
                IsPublic = true,
                DeclaringType = source.DeclaringType,
                PropertyName = source.Name,

                // source.Name is already the effective JSON name; setting it here keeps the naming
                // policy from being applied a second time.
                JsonPropertyName = source.Name,
                Converter = converter,

                // Supplied rather than resolved: a source-generated context has metadata for T but
                // not for Nullable<T>, so leaving this null would fail contract configuration.
                PropertyTypeInfo = JsonMetadataServices.CreateValueInfo<T?>(options, converter),
                NumberHandling = source.NumberHandling,
            });
    }
}
