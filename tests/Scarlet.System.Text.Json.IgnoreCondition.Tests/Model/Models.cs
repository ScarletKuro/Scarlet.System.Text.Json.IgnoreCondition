using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

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
/// A settable member whose JSON name equals a constructor parameter name. The serializer binds that
/// parameter by CLR name to <see cref="Title"/>, so <see cref="Subtitle"/> must go through its setter.
/// </summary>
public sealed class JsonNameCollisionModel
{
    [JsonConstructor]
    public JsonNameCollisionModel(string title) => Title = title;

    [JsonPropertyName("heading")]
    public string Title { get; }

    [JsonPropertyName("title")]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Subtitle { get; set; } = "initial";
}

/// <summary>WhenReadingDefault on a constructor parameter: an incoming 0 also falls back to the parameter default.</summary>
public sealed record DefaultParameterRecord(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)] int Age = 41);

/// <summary>An enum constructor parameter, whose default value reflection reports as the underlying integer.</summary>
public sealed record EnumParameterRecord(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] Season Season = Season.Summer);

/// <summary>A constructor parameter with no default, which can only fall back to the type default.</summary>
public sealed record RequiredParameterRecordModel(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string Name);

public sealed class WriteModel
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WhenWritingNull { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int WhenWritingDefault { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public string WhenWriting { get; set; } = "written";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenReading)]
    public string WhenReading { get; set; } = "read";

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string Always { get; set; } = "always";

    public string Kept { get; set; } = "kept";
}

/// <summary>One read-side and one write-side condition on the same member.</summary>
public sealed class CombinedModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; } = "initial";
}

/// <summary>Read conditions must preserve the original value type's write defaults.</summary>
public sealed class ProjectedWriteDefaultsModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Count { get; set; }

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)]
    public bool Enabled { get; set; }

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public Guid Id { get; set; }

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int? Optional { get; set; } = 0;

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int AlwaysWritten { get; set; }
}

public sealed class NonNullableReferenceModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string Name { get; set; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)]
    public List<string> Values { get; set; } = ["initial"];

    [JsonInclude]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string Field = "initial";

    public string Untouched { get; set; } = "initial";
}

public sealed class ReadingDefaultModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)]
    public string? Name { get; set; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)]
    public int Age { get; set; } = 41;

    /// <summary>The default of <c>int?</c> is null, so an incoming 0 is assigned.</summary>
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)]
    public int? Optional { get; set; } = 7;
}

public sealed class MixedConditionModel
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Name { get; set; } = "initial";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Count { get; set; } = 7;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenReading)]
    public string? ReadIgnored { get; set; } = "initial";

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int AlwaysWritten { get; set; } = 9;


}

public sealed class NativePrecedenceModel
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public string? NeverWritten { get; set; } = "initial";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenReading)]
    public string? NeverRead { get; set; } = "initial";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Count { get; set; }
}


public sealed class ConflictingNativeReadModel
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenReading)]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Value { get; set; }
}

/// <summary>Carries no attributes; used to exercise the options-level defaults.</summary>
public sealed class OptionsDefaultModel
{
    public string? Name { get; set; } = "initial";

    public List<string> Values { get; set; } = ["a"];

    public int? Optional { get; set; } = 7;

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? OptedOut { get; set; } = "initial";
}

/// <summary>
/// Compares the obsolete <c>IgnoreNullValues</c> with its replacement: one member of each shape the two
/// treat differently on the read side.
/// </summary>
public sealed class IgnoreNullValuesModel
{
    public string? Name { get; set; } = "initial";

    public List<string> Values { get; set; } = ["a"];

    public int? Optional { get; set; } = 7;

    public int Count { get; set; } = 3;

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? OptedOut { get; set; } = "initial";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WriteConditioned { get; set; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Annotated { get; set; } = 5;
}

/// <summary>Unannotated constructor parameters, for the same comparison.</summary>
public sealed record IgnoreNullValuesRecord(string? Name = "unknown", int Age = 41);

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

/// <summary>A struct with its own members, so the projection has to reuse a non-primitive converter.</summary>
public readonly record struct Point(int X, int Y);

/// <summary>A value type the library's rooted table does not know about.</summary>
public enum Priority
{
    Low,
    Normal,
    High,
}

/// <summary>
/// Member-level converter factories. The reflection resolver leaves the factory itself in
/// <c>CustomConverter</c>, where the source generator emits the converter it creates.
/// </summary>
public sealed class ConverterFactoryModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    [JsonConverter(typeof(JsonStringEnumConverter<Season>))]
    public Season Season { get; set; } = Season.Autumn;
}

public sealed record ConverterFactoryRecord(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    [property: JsonConverter(typeof(JsonStringEnumConverter<Season>))]
    Season Season = Season.Autumn);

public sealed record ObjectPayloadModel(
    object First,
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] object? Payload = null);

public readonly record struct ArrayBox<T>(T Value);

public sealed class ArrayArgumentModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public ArrayBox<int[]> Box { get; set; } = new([1, 2]);
}

/// <summary>
/// Init-only members. The reflection resolver assigns them through the init accessor; the source generator
/// assigns them through an object initializer, which bypasses any setter.
/// </summary>
public sealed class InitOnlyModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Name { get; init; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Count { get; init; } = 3;
}

/// <summary>Init-only, without an attribute: reached only by an options-level default.</summary>
public sealed class UnannotatedInitOnlyModel
{
    public string? Name { get; init; } = "initial";

    /// <summary>A non-nullable value type: re-declaring it as T? would break the generated initializer binding.</summary>
    public int Count { get; init; } = 3;
}

/// <summary>
/// Nullable-enum constructor parameters with a default. Reflection reports that default as the underlying
/// integer, which does not unbox to <c>Season?</c>.
/// </summary>
public sealed record NullableEnumParameterRecord(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] Season? Season = Season.Autumn);

public sealed record UnannotatedNullableEnumRecord(Season? Season = Season.Autumn);

/// <summary>A converter that reads and writes JSON null itself (HandleNull), as a sentinel.</summary>
public sealed class NullAsTextConverter : JsonConverter<string?>
{
    public override bool HandleNull => true;

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? "from-null" : reader.GetString();

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value ?? "none");
}

/// <summary>The same for <see cref="Nullable{T}"/>: reads null as -1, writes null as -1.</summary>
public sealed class NullAsMinusOneConverter : JsonConverter<int?>
{
    public override bool HandleNull => true;

    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? -1 : reader.GetInt32();

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value ?? -1);
}

public sealed class NullHandlingConverterModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    [JsonConverter(typeof(NullAsTextConverter))]
    public string? Name { get; set; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    [JsonConverter(typeof(NullAsMinusOneConverter))]
    public int? Count { get; set; } = 3;
}

public sealed record NullHandlingConverterRecord(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    [property: JsonConverter(typeof(NullAsTextConverter))]
    string? Name = "initial");

/// <summary>Reads JSON null as -100: registered in the options for <c>int</c>, not on any member.</summary>
public sealed class NullAsMinusHundredConverter : JsonConverter<int>
{
    public override bool HandleNull => true;

    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? -100 : reader.GetInt32();

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

/// <summary>
/// Annotated members without member converters. The tests register null-handling converters in the options,
/// which the serializer then uses for these members.
/// </summary>
public sealed class OptionsConverterModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Name { get; set; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int? Optional { get; set; } = 3;

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Count { get; set; } = 5;
}

/// <summary>A type whose own <c>[JsonConverter]</c> reads JSON null as a value.</summary>
[JsonConverter(typeof(TokenConverter))]
public sealed record Token(string Text);

public sealed class TokenConverter : JsonConverter<Token>
{
    public override bool HandleNull => true;

    public override Token Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.TokenType == JsonTokenType.Null ? "from-null" : reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, Token value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Text);
}

/// <summary>A factory, registered in the options, whose converter reads JSON null itself.</summary>
public sealed class NullAsTextConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(string);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        new NullAsTextConverter();
}

public sealed class TypeLevelConverterModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public Token? Token { get; set; } = new("initial");
}

/// <summary>A converter for a base type that accepts int, registered in the options.</summary>
public sealed class BaseTypeOptionsConverter : JsonConverter<object>
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(int);

    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetInt32();

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
        writer.WriteNumberValue((int)value);
}

/// <summary>A converter for a base type that accepts int and reads JSON null itself, as -1.</summary>
public sealed class BaseTypeNullIntOptionsConverter : JsonConverter<object>
{
    public override bool HandleNull => true;

    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(int);

    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? -1 : reader.GetInt32();

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
        writer.WriteNumberValue((int)value);
}

/// <summary>A non-nullable value type, so the member is re-declared as T? around the converter in effect.</summary>
public sealed class OptionsBaseNullConverterModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Count { get; set; } = 5;
}

public sealed record OptionsBaseConverterRecord(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] int Count = 9);

/// <summary>A user enum that is deliberately never registered, not even for the NativeAOT run.</summary>
public enum Shade
{
    Light,
    Dark,
}

/// <summary>Carries no attributes; reached only by an options-level default.</summary>
public sealed class UnregisteredValueTypeModel
{
    public Shade Shade { get; set; } = Shade.Dark;
}

/// <summary>An unregistered closed generic struct, reached only by an options-level default.</summary>
public sealed class UnregisteredGenericModel
{
    public ArrayBox<int> Box { get; set; }
}

/// <summary>The same unregistered type behind <see cref="Nullable{T}"/>.</summary>
public sealed class UnregisteredNullableValueTypeModel
{
    public Shade? Tint { get; set; } = Shade.Dark;
}

/// <summary>Used by no model, so only an explicit RegisterValueType call can register it.</summary>
public enum ManualOnly
{
    A,
}

/// <summary>A .NET value type outside the library's rooted set: registered by the generator, like a user enum.</summary>
public sealed class FrameworkValueTypeModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public DayOfWeek Day { get; set; } = DayOfWeek.Friday;
}

public sealed class CustomValueTypeModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public Point Location { get; set; } = new(3, 4);

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public Priority Priority { get; set; } = Priority.High;
}

/// <summary>A generic model: the resolver reads attributes per closed contract, so this just works.</summary>
public sealed class GenericModel<T>
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public T? Value { get; set; }

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int Count { get; set; } = 5;
}

/// <summary>[JsonIgnore] removes the member before the resolver ever sees it.</summary>
public sealed class IgnoredMemberModel
{
    [JsonIgnore]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Hidden { get; set; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Visible { get; set; } = "initial";
}
