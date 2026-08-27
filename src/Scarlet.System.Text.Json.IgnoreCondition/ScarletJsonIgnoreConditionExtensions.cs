using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Scarlet.Text.Json.Internal;

namespace Scarlet.Text.Json.Serialization;

/// <summary>
/// Installs the contract customization that makes <see cref="JsonIgnoreExAttribute"/> take effect.
/// </summary>
public static class ScarletJsonIgnoreConditionExtensions
{
    private static readonly ConditionalWeakTable<JsonSerializerOptions, object> AlreadyInstalled = [];

    /// <summary>
    /// Adds extended ignore-condition support to <paramref name="options"/>, resolving contracts
    /// through reflection if no <see cref="JsonSerializerOptions.TypeInfoResolver"/> has been set.
    /// </summary>
    /// <param name="options">The options to configure. Must not yet be in use.</param>
    /// <param name="settings">Optional settings; defaults are used when omitted.</param>
    /// <returns>The same <paramref name="options"/> instance, for chaining.</returns>
    /// <remarks>
    /// Calling this more than once on the same instance has no additional effect. For NativeAOT or
    /// trimmed applications use <see cref="WithExtendedIgnoreConditions"/> on a source-generated
    /// <c>JsonSerializerContext</c> instead, which needs no reflection.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The options instance is already in use.</exception>
    [RequiresUnreferencedCode("Falls back to DefaultJsonTypeInfoResolver when no resolver is configured.")]
    [RequiresDynamicCode("Falls back to DefaultJsonTypeInfoResolver when no resolver is configured.")]
    public static JsonSerializerOptions AddExtendedIgnoreConditions(
        this JsonSerializerOptions options,
        ExtendedIgnoreConditionSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (AlreadyInstalled.TryGetValue(options, out _))
        {
            return options;
        }

        IJsonTypeInfoResolver resolver = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
        options.TypeInfoResolver = resolver.WithExtendedIgnoreConditions(settings);
        AlreadyInstalled.Add(options, AlreadyInstalled);

        return options;
    }

    /// <summary>
    /// Wraps a contract resolver so that the metadata it produces honours
    /// <see cref="JsonIgnoreExAttribute"/>.
    /// </summary>
    /// <param name="resolver">The resolver to wrap, typically a generated <c>JsonSerializerContext</c>.</param>
    /// <param name="settings">Optional settings; defaults are used when omitted.</param>
    /// <returns>A resolver that applies the extended conditions.</returns>
    /// <example>
    /// <code>
    /// options.TypeInfoResolver = MyContext.Default.WithExtendedIgnoreConditions();
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException"><paramref name="resolver"/> is <see langword="null"/>.</exception>
    public static IJsonTypeInfoResolver WithExtendedIgnoreConditions(
        this IJsonTypeInfoResolver resolver,
        ExtendedIgnoreConditionSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        return resolver.WithAddedModifier(CreateModifier(settings));
    }

    /// <summary>
    /// Creates the contract modifier directly, for adding to
    /// <see cref="DefaultJsonTypeInfoResolver.Modifiers"/> alongside other customizations.
    /// </summary>
    /// <param name="settings">Optional settings; defaults are used when omitted.</param>
    /// <returns>A modifier that applies the extended conditions to a resolved contract.</returns>
    public static Action<JsonTypeInfo> CreateModifier(ExtendedIgnoreConditionSettings? settings = null) =>
        new ExtendedIgnoreConditionModifier(settings ?? ExtendedIgnoreConditionSettings.Default).Modify;
}
