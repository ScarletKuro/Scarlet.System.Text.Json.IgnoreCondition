namespace Scarlet.System.Text.Json.IgnoreCondition;

/// <summary>Read-side ignore conditions that System.Text.Json does not provide.</summary>
/// <remarks>Use the native JsonIgnoreAttribute for standard read and write conditions.</remarks>
public enum JsonIgnoreConditionEx
{
    /// <summary>
    /// Ignore an incoming JSON null, preserving the member's initializer or constructor parameter default.
    /// For non-nullable value types, this also prevents the usual exception for JSON null.
    /// </summary>
    WhenReadingNull = 0,

    /// <summary>
    /// Ignore an incoming JSON null or type default, preserving the member's initializer or constructor parameter default.
    /// The default is that of the declared member type, so for a <see cref="Nullable{T}"/> member it is
    /// <see langword="null"/>: <c>int?</c> skips <c>null</c> but assigns <c>0</c>.
    /// </summary>
    WhenReadingDefault = 1,
}
