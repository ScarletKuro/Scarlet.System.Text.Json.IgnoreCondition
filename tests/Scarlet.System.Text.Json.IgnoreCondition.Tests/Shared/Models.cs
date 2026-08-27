using System.Text.Json.Serialization;
using Scarlet.Text.Json.Serialization;

namespace Scarlet.Text.Json.Tests;

public enum Season
{
    Winter,
    Spring,
    Summer,
    Autumn,
}

/// <summary>Members whose type already accepts null, so the setter alone is intercepted.</summary>
public sealed class ReferenceModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Name { get; set; } = "initial";

    /// <summary>The dotnet/runtime#62086 scenario: a collection with an initializer.</summary>
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public List<string> Values { get; set; } = ["a"];

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int? Optional { get; set; } = 7;

    public string? Untouched { get; set; } = "initial";
}

/// <summary>
/// Members the serializer refuses to read a JSON null into, which the contract re-declares as
/// <c>T?</c> (dotnet/runtime#74008 and #83706).
/// </summary>
public sealed class ValueModel
{
    public static readonly Guid KnownId = new("2f8e0a4c-1d3b-4f5e-9a7c-6b8d0e2f4a61");

    public static readonly DateTime KnownTimestamp = new(2026, 8, 27, 13, 45, 0, DateTimeKind.Utc);

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Age { get; set; } = 41;

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public Guid Id { get; set; } = KnownId;

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public DateTime Timestamp { get; set; } = KnownTimestamp;

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public Season Season { get; set; } = Season.Summer;

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public decimal Amount { get; set; } = 12.5m;
}

/// <summary>A field rather than a property, to prove the mechanism is accessor-based.</summary>
public sealed class FieldModel
{
    [JsonInclude]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Name = "initial";

    [JsonInclude]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Age = 41;
}

/// <summary>A struct target, so the setter runs against a boxed instance.</summary>
public struct StructModel
{
    public StructModel()
    {
    }

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Name { get; set; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Age { get; set; } = 41;
}

/// <summary>
/// A record whose members are bound to constructor parameters, so the condition substitutes each
/// parameter's own default value.
/// </summary>
public sealed record RecordModel(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string Name = "unknown",
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] int Age = 41,
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string? Nickname = null);

/// <summary>
/// The same, annotated directly on the positional parameters, which is where the compiler puts the
/// attribute when no <c>property:</c> target is given.
/// </summary>
public sealed record ParameterRecordModel(
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string Name = "unknown",
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] int Age = 41);

/// <summary>A constructor parameter with no default, which can only fall back to the type default.</summary>
public sealed record RequiredParameterRecordModel(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string Name);

public sealed class WriteModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenWritingNull)]
    public string? WhenWritingNull { get; set; }

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenWritingDefault)]
    public int WhenWritingDefault { get; set; }

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenWriting)]
    public string WhenWriting { get; set; } = "written";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReading)]
    public string WhenReading { get; set; } = "read";

    [JsonIgnoreEx(JsonIgnoreConditionEx.Always)]
    public string Always { get; set; } = "always";

    public string Kept { get; set; } = "kept";
}

/// <summary>One read-side and one write-side condition on the same member.</summary>
public sealed class CombinedModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenWritingNull)]
    public string? Name { get; set; } = "initial";
}

public sealed class ReadingDefaultModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)]
    public string? Name { get; set; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)]
    public int Age { get; set; } = 41;
}

/// <summary>Carries no attributes; used to exercise the options-level defaults.</summary>
public sealed class OptionsDefaultModel
{
    public string? Name { get; set; } = "initial";

    public List<string> Values { get; set; } = ["a"];

    public int? Optional { get; set; } = 7;

    [JsonIgnoreEx(JsonIgnoreConditionEx.Never)]
    public string? OptedOut { get; set; } = "initial";
}

/// <summary>A nested graph, large enough to force read continuations when streamed.</summary>
public sealed class NestedModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Name { get; set; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Age { get; set; } = 41;

    public List<NestedModel> Children { get; set; } = [];

    public string Padding { get; set; } = string.Empty;
}

/// <summary>An annotated base class surfaced through a derived contract.</summary>
public class BaseModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Name { get; set; } = "initial";
}

public sealed class DerivedModel : BaseModel
{
    public string? Extra { get; set; } = "extra";
}

/// <summary>Renamed and policy-mapped members, to check the contract entries are matched correctly.</summary>
public sealed class RenamedModel
{
    [JsonPropertyName("display_name")]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Name { get; set; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int RetryCount { get; set; } = 3;
}

/// <summary>Has no [JsonIgnoreEx] at all: the contract must be left untouched.</summary>
public sealed class UnannotatedModel
{
    public string? Name { get; set; } = "initial";

    public int Age { get; set; } = 41;
}

/// <summary>Polymorphic hierarchy: the condition must survive the type-discriminator machinery.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(CatShape), "cat")]
[JsonDerivedType(typeof(DogShape), "dog")]
public abstract class AnimalShape
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Name { get; set; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Age { get; set; } = 41;
}

public sealed class CatShape : AnimalShape
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public bool Indoor { get; set; } = true;
}

public sealed class DogShape : AnimalShape
{
    public string Breed { get; set; } = "unknown";
}

/// <summary>Used with <see cref="ReferenceHandler.Preserve"/>.</summary>
public sealed class GraphNode
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Name { get; set; } = "initial";

    public GraphNode? Next { get; set; }
}

/// <summary>A member the serializer is asked to populate rather than replace.</summary>
public sealed class PopulateModel
{
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public List<string> Values { get; set; } = ["a"];
}

/// <summary>A numeric member with non-default number handling, read through the nullable projection.</summary>
public sealed class NumberHandlingModel
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Count { get; set; } = 3;

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public double Ratio { get; set; } = 1.5;
}
