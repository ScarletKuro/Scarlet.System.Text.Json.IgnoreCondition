using Scarlet.System.Text.Json.IgnoreCondition.Internal;

namespace Scarlet.System.Text.Json.IgnoreCondition;

/// <summary>
/// Configures how <see cref="ExtendedIgnoreConditionResolver"/> applies
/// <see cref="JsonIgnoreExAttribute"/>.
/// </summary>
public sealed class ExtendedIgnoreConditionSettings
{
    private JsonIgnoreConditionEx? _defaultReadCondition;
    private JsonIgnoreConditionEx? _defaultWriteCondition;

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
    /// Individual members opt out with <see cref="JsonIgnoreConditionEx.Never"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a read-side condition.</exception>
    public JsonIgnoreConditionEx? DefaultReadCondition
    {
        get => _defaultReadCondition;
        set
        {
            if (value is { } condition &&
                condition != JsonIgnoreConditionEx.Never &&
                !ConditionKinds.IsReadSide(condition))
            {
                throw new ArgumentOutOfRangeException(nameof(value), condition, "Not a read-side condition.");
            }

            _defaultReadCondition = value;
        }
    }

    /// <summary>
    /// Gets or sets a write-side condition applied to every member that does not carry an explicit
    /// <see cref="JsonIgnoreExAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Equivalent to <c>JsonSerializerOptions.DefaultIgnoreCondition</c>; provided so that both sides
    /// can be configured through one mechanism.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a write-side condition.</exception>
    public JsonIgnoreConditionEx? DefaultWriteCondition
    {
        get => _defaultWriteCondition;
        set
        {
            if (value is { } condition &&
                condition != JsonIgnoreConditionEx.Never &&
                !ConditionKinds.IsWriteSide(condition))
            {
                throw new ArgumentOutOfRangeException(nameof(value), condition, "Not a write-side condition.");
            }

            _defaultWriteCondition = value;
        }
    }

    /// <summary>
    /// Gets or sets what happens when a condition cannot be applied to a member. The default is
    /// <see cref="UnsupportedMemberBehavior.Throw"/>.
    /// </summary>
    public UnsupportedMemberBehavior UnsupportedMemberBehavior { get; set; } = UnsupportedMemberBehavior.Throw;
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
