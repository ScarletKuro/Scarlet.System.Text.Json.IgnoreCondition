namespace Scarlet.Text.Json.Serialization;

/// <summary>
/// Controls how a property or field is ignored during serialization and deserialization.
/// </summary>
/// <remarks>
/// Mirrors <see cref="global::System.Text.Json.Serialization.JsonIgnoreCondition"/> and adds the
/// read-side conditions that <c>System.Text.Json</c> does not provide
/// (<see cref="WhenReadingNull"/> and <see cref="WhenReadingDefault"/>).
/// The numeric values of the mirrored members match the built-in enum.
/// </remarks>
public enum JsonIgnoreConditionEx
{
    /// <summary>
    /// The member is never ignored. Clears any condition inherited from
    /// <see cref="ExtendedIgnoreConditionSettings.DefaultReadCondition"/> or
    /// <see cref="ExtendedIgnoreConditionSettings.DefaultWriteCondition"/>.
    /// </summary>
    Never = 0,

    /// <summary>The member is always ignored, for both reading and writing.</summary>
    Always = 1,

    /// <summary>The member is ignored when writing if its value is null or the type default.</summary>
    WhenWritingDefault = 2,

    /// <summary>The member is ignored when writing if its value is null.</summary>
    WhenWritingNull = 3,

    /// <summary>The member is always ignored when writing, but is still deserialized.</summary>
    WhenWriting = 4,

    /// <summary>The member is always ignored when reading, but is still serialized.</summary>
    WhenReading = 5,

    /// <summary>
    /// The incoming JSON value is ignored when reading if it is null, leaving the member at whatever
    /// value the object was constructed with.
    /// </summary>
    /// <remarks>
    /// This is the condition proposed in
    /// <see href="https://github.com/dotnet/runtime/issues/90011">dotnet/runtime#90011</see>.
    /// For a non-nullable value type it also suppresses the exception that <c>System.Text.Json</c>
    /// would otherwise throw for an explicit JSON null
    /// (<see href="https://github.com/dotnet/runtime/issues/74008">dotnet/runtime#74008</see>).
    /// </remarks>
    WhenReadingNull = 6,

    /// <summary>
    /// The incoming JSON value is ignored when reading if it is null or the type default, leaving the
    /// member at whatever value the object was constructed with.
    /// </summary>
    WhenReadingDefault = 7,
}
