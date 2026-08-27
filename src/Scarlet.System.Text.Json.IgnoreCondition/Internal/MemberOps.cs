using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Scarlet.Text.Json.Internal.Converters;

namespace Scarlet.Text.Json.Internal;

/// <summary>
/// The operations on a member that need the member type as a generic argument rather than as a
/// <see cref="Type"/> value.
/// </summary>
/// <remarks>
/// The source generator hands the runtime an already-instantiated <see cref="MemberOps{T}"/> or
/// <see cref="ValueMemberOps{T}"/>, so nothing on the source-generated path needs
/// <see cref="Type.MakeGenericType"/> or <see cref="Activator"/> and it stays NativeAOT-clean. Only
/// the reflection fallback goes through <see cref="Create"/>.
/// </remarks>
internal abstract class MemberOps
{
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

    [RequiresUnreferencedCode("Instantiates MemberOps<T> for a member type only known at run time.")]
    [RequiresDynamicCode("Instantiates MemberOps<T> for a member type only known at run time.")]
    public static MemberOps Create(Type memberType)
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
    public static new readonly ValueMemberOps<T> Instance = new();

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
