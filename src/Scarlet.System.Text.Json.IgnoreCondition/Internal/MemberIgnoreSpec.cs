using Scarlet.Text.Json.Serialization;

namespace Scarlet.Text.Json.Internal;

/// <summary>The resolved ignore conditions for a single member, from either the registry or reflection.</summary>
internal sealed class MemberIgnoreSpec
{
    public required string MemberName { get; init; }

    public required MemberOps Ops { get; init; }

    /// <summary>
    /// The type the entry was registered for, or <see langword="null"/> when the entry was discovered
    /// by reflection from a contract that already contains the member.
    /// </summary>
    public Type? DeclaringType { get; init; }

    /// <summary>The read-side condition, or <see langword="null"/> when reads are unaffected.</summary>
    public JsonIgnoreConditionEx? ReadCondition { get; init; }

    /// <summary>The write-side condition, or <see langword="null"/> when writes are unaffected.</summary>
    public JsonIgnoreConditionEx? WriteCondition { get; init; }

    /// <summary>Whether the member carries <see cref="JsonIgnoreConditionEx.Always"/>.</summary>
    public bool IgnoreAlways { get; init; }

    /// <summary>Whether the member explicitly opted out with <see cref="JsonIgnoreConditionEx.Never"/>.</summary>
    public bool SuppressReadDefault { get; init; }

    public bool SuppressWriteDefault { get; init; }
}
