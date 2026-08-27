using System.Collections.Concurrent;
using Scarlet.Text.Json.Internal;

namespace Scarlet.Text.Json.Serialization.Metadata;

/// <summary>
/// Holds the compile-time ignore-condition metadata for annotated types, so that
/// <see cref="JsonIgnoreExAttribute"/> can be honoured without reading attributes by reflection.
/// </summary>
/// <remarks>
/// The Scarlet source generator emits one registration per annotated type and calls it from a module
/// initializer, so ordinary use never touches this type directly. Registering by hand is supported
/// for assemblies that disable the generator.
/// </remarks>
public static class ExtendedIgnoreConditionRegistry
{
    private static readonly ConcurrentDictionary<Type, MemberIgnoreSpec[]> Registrations = new();

    /// <summary>Registers the ignore conditions declared by <paramref name="type"/>.</summary>
    /// <param name="type">The type whose members carry ignore conditions.</param>
    /// <param name="configure">A callback that declares one entry per annotated member.</param>
    /// <remarks>
    /// Registration replaces any previous entry for the same type. It must happen before the type is
    /// first serialized, because <c>System.Text.Json</c> caches the resolved contract.
    /// </remarks>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void Register(Type type, Action<IIgnoreConditionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new IgnoreConditionBuilder();
        configure(builder);
        Registrations[type] = builder.Build(type);
    }

    /// <summary>
    /// Returns <see langword="true"/> when compile-time metadata is available for
    /// <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The type to query.</param>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
    public static bool IsRegistered(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Registrations.ContainsKey(type);
    }

    internal static MemberIgnoreSpec[]? TryGetSpecs(Type type) =>
        Registrations.TryGetValue(type, out MemberIgnoreSpec[]? specs) ? specs : null;
}
