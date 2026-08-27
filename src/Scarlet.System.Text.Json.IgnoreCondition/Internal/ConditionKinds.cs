using Scarlet.Text.Json.Serialization;

namespace Scarlet.Text.Json.Internal;

/// <summary>Classifies <see cref="JsonIgnoreConditionEx"/> members by the side they act on.</summary>
internal static class ConditionKinds
{
    public static bool IsReadSide(JsonIgnoreConditionEx condition) => condition
        is JsonIgnoreConditionEx.WhenReading
        or JsonIgnoreConditionEx.WhenReadingNull
        or JsonIgnoreConditionEx.WhenReadingDefault;

    public static bool IsWriteSide(JsonIgnoreConditionEx condition) => condition
        is JsonIgnoreConditionEx.WhenWriting
        or JsonIgnoreConditionEx.WhenWritingNull
        or JsonIgnoreConditionEx.WhenWritingDefault;

    /// <summary>
    /// Returns the condition to store for the read side, or <see langword="null"/> when the condition
    /// does not act on reads. <see cref="JsonIgnoreConditionEx.Never"/> is normalised to
    /// <see langword="null"/> by the caller after it has cleared any inherited default.
    /// </summary>
    public static JsonIgnoreConditionEx? ReadSideOf(JsonIgnoreConditionEx condition) =>
        IsReadSide(condition) ? condition : null;

    public static JsonIgnoreConditionEx? WriteSideOf(JsonIgnoreConditionEx condition) =>
        IsWriteSide(condition) ? condition : null;
}
