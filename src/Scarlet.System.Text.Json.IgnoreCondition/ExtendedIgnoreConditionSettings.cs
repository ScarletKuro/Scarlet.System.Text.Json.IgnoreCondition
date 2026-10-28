namespace Scarlet.System.Text.Json.IgnoreCondition;

/// <summary>
/// Configures how <see cref="ExtendedIgnoreConditionResolver"/> applies
/// <see cref="JsonIgnoreExAttribute"/>.
/// </summary>
/// <remarks>
/// The values are captured when the resolver is constructed; changing them afterwards has no effect on
/// that resolver.
/// </remarks>
public sealed class ExtendedIgnoreConditionSettings
{
    private JsonIgnoreConditionEx? _defaultReadCondition;

    internal static ExtendedIgnoreConditionSettings Default { get; } = new();

    /// <summary>
    /// Gets or sets a read-side condition applied to every member that does not carry an explicit
    /// <see cref="JsonIgnoreExAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Setting this to <see cref="JsonIgnoreConditionEx.WhenReadingNull"/> restores the read-side
    /// behaviour of the obsolete <c>JsonSerializerOptions.IgnoreNullValues</c>, which
    /// <c>DefaultIgnoreCondition = WhenWritingNull</c> deliberately does not provide
    /// (<see href="https://github.com/dotnet/runtime/issues/62086">dotnet/runtime#62086</see>).
    /// Individual members opt out with native JsonIgnore(Condition = JsonIgnoreCondition.Never).
    /// An explicit JsonIgnoreEx condition takes precedence over that opt-out.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a read-side condition.</exception>
    public JsonIgnoreConditionEx? DefaultReadCondition
    {
        get => _defaultReadCondition;
        set
        {
            if (value is { } condition &&
                condition is not (JsonIgnoreConditionEx.WhenReadingNull or JsonIgnoreConditionEx.WhenReadingDefault))
            {
                throw new ArgumentOutOfRangeException(nameof(value), condition, "Not a read-side condition.");
            }

            _defaultReadCondition = value;
        }
    }

    /// <summary>
    /// Gets or sets what happens when a condition cannot be applied to a member. The default is
    /// <see cref="UnsupportedMemberBehavior.Throw"/>.
    /// </summary>
    public UnsupportedMemberBehavior UnsupportedMemberBehavior { get; set; } = UnsupportedMemberBehavior.Throw;

    /// <summary>
    /// Gets or sets whether a custom value type must be registered even where the runtime could create
    /// its metadata on demand. The default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Under NativeAOT a custom enum or struct needs
    /// <see cref="ExtendedIgnoreConditionResolver.RegisterValueType{T}"/>. Members carrying
    /// <see cref="JsonIgnoreExAttribute"/> are registered automatically by the bundled source generator;
    /// members reached only by <see cref="DefaultReadCondition"/>, or through a generic type parameter,
    /// are not. Under JIT a missing registration goes unnoticed because the runtime fills the gap.
    /// </para>
    /// <para>
    /// Set this to <see langword="true"/> in an application that targets, or will target, NativeAOT, so
    /// that JIT runs refuse unregistered value types exactly as a native build would.
    /// </para>
    /// </remarks>
    public bool RequireValueTypeRegistration { get; set; }
}

/// <summary>Describes what happens when an ignore condition cannot be applied to a member.</summary>
public enum UnsupportedMemberBehavior
{
    /// <summary>
    /// Throw <see cref="NotSupportedException"/> while the contract is being resolved, naming the
    /// member and the reason.
    /// </summary>
    Throw = 0,

    /// <summary>Leave the built-in behaviour of the member untouched.</summary>
    Skip = 1,
}
