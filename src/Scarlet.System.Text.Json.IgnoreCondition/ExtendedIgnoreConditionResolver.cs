using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Scarlet.Text.Json.Internal;

namespace Scarlet.Text.Json.Serialization;

/// <summary>
/// A contract resolver that makes <see cref="JsonIgnoreExAttribute"/> take effect, by applying the
/// extended ignore conditions to the metadata produced by another resolver.
/// </summary>
/// <remarks>
/// <para>
/// Wrap a source-generated <c>JsonSerializerContext</c> to keep serialization reflection-free, or use
/// the parameterless constructor to resolve contracts by reflection.
/// </para>
/// <para>
/// The attribute is read from <see cref="JsonPropertyInfo.AttributeProvider"/>, which both the
/// reflection-based resolver and the <c>System.Text.Json</c> source generator populate, so one
/// implementation serves both.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Source-generated, including NativeAOT
/// var options = new JsonSerializerOptions
/// {
///     TypeInfoResolver = new ExtendedIgnoreConditionResolver(MyJsonContext.Default),
/// };
///
/// // Reflection-based
/// var options = new JsonSerializerOptions
/// {
///     TypeInfoResolver = new ExtendedIgnoreConditionResolver(),
/// };
/// </code>
/// </example>
public sealed class ExtendedIgnoreConditionResolver : IJsonTypeInfoResolver
{
    private readonly IJsonTypeInfoResolver _source;
    private readonly ExtendedIgnoreConditionModifier _modifier;

    /// <summary>
    /// Initializes a resolver that applies the extended conditions to metadata from
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The resolver producing the metadata, typically a generated context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public ExtendedIgnoreConditionResolver(IJsonTypeInfoResolver source)
        : this(source, ExtendedIgnoreConditionSettings.Default)
    {
    }

    /// <summary>
    /// Initializes a resolver that applies the extended conditions to metadata from
    /// <paramref name="source"/>, with the specified settings.
    /// </summary>
    /// <param name="source">The resolver producing the metadata, typically a generated context.</param>
    /// <param name="settings">Options-level defaults and error behaviour.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ExtendedIgnoreConditionResolver(IJsonTypeInfoResolver source, ExtendedIgnoreConditionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);

        _source = source;
        _modifier = new ExtendedIgnoreConditionModifier(settings);
    }

    /// <summary>Initializes a resolver that resolves contracts by reflection.</summary>
    [RequiresUnreferencedCode("Resolves contracts with DefaultJsonTypeInfoResolver. Pass a source-generated context instead.")]
    [RequiresDynamicCode("Resolves contracts with DefaultJsonTypeInfoResolver. Pass a source-generated context instead.")]
    public ExtendedIgnoreConditionResolver()
        : this(new DefaultJsonTypeInfoResolver(), ExtendedIgnoreConditionSettings.Default)
    {
    }

    /// <summary>
    /// Initializes a resolver that resolves contracts by reflection, with the specified settings.
    /// </summary>
    /// <param name="settings">Options-level defaults and error behaviour.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode("Resolves contracts with DefaultJsonTypeInfoResolver. Pass a source-generated context instead.")]
    [RequiresDynamicCode("Resolves contracts with DefaultJsonTypeInfoResolver. Pass a source-generated context instead.")]
    public ExtendedIgnoreConditionResolver(ExtendedIgnoreConditionSettings settings)
        : this(new DefaultJsonTypeInfoResolver(), settings)
    {
    }

    /// <summary>
    /// Makes the metadata for a value type available to the extended conditions on a platform that
    /// cannot create generic instantiations at run time.
    /// </summary>
    /// <typeparam name="T">A non-nullable value type used by an annotated member.</typeparam>
    /// <remarks>
    /// Only needed under NativeAOT, and only for a custom enum or struct: the built-in value types are
    /// already available. Call it once during startup, before the first serialization.
    /// </remarks>
    /// <example>
    /// <code>
    /// ExtendedIgnoreConditionResolver.RegisterValueType&lt;LogLevel&gt;();
    /// </code>
    /// </example>
    public static void RegisterValueType<T>()
        where T : struct => MemberOps.Register<T>();

    /// <inheritdoc />
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        JsonTypeInfo? typeInfo = _source.GetTypeInfo(type, options);
        if (typeInfo is null)
        {
            return null;
        }

        _modifier.Modify(typeInfo);

        return typeInfo;
    }
}
