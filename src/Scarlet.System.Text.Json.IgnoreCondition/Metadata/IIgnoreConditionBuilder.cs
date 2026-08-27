namespace Scarlet.Text.Json.Serialization.Metadata;

/// <summary>
/// Declares the ignore conditions of a type's members without reflection. Implemented by the library
/// and called from code emitted by the Scarlet source generator; also usable by hand.
/// </summary>
/// <remarks>
/// Each method takes the member type as a generic argument so that the library can build the
/// converters and nullable projections it needs without <see cref="Type.MakeGenericType"/>, which is
/// what keeps the source-generated path trim- and NativeAOT-safe.
/// </remarks>
public interface IIgnoreConditionBuilder
{
    /// <summary>
    /// Declares a condition for a member whose type is a reference type or a
    /// <see cref="Nullable{T}"/>.
    /// </summary>
    /// <typeparam name="TMember">The declared type of the member.</typeparam>
    /// <param name="memberName">The CLR name of the property or field.</param>
    /// <param name="condition">The condition to apply.</param>
    void Member<TMember>(string memberName, JsonIgnoreConditionEx condition);

    /// <summary>
    /// Declares a condition for a member whose type is a non-nullable value type.
    /// </summary>
    /// <typeparam name="TMember">The declared type of the member.</typeparam>
    /// <param name="memberName">The CLR name of the property or field.</param>
    /// <param name="condition">The condition to apply.</param>
    /// <remarks>
    /// The <c>struct</c> constraint gives the library a statically instantiated
    /// <c>Nullable&lt;TMember&gt;</c>, which is how a read-side condition is applied to a member that
    /// the serializer would otherwise refuse to read a JSON null into.
    /// </remarks>
    void ValueMember<TMember>(string memberName, JsonIgnoreConditionEx condition)
        where TMember : struct;
}
