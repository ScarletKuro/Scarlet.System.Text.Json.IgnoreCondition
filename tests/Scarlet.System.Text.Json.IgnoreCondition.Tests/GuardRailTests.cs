using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>
/// Cases where a read-side condition cannot be honoured. Each one has to fail loudly, naming the member
/// and the reason, rather than silently doing nothing.
/// </summary>
/// <remarks>
/// These run against the reflection resolver only: the mechanism selection lives in the contract
/// modifier and is resolver-independent, and the models are deliberately absent from the generated
/// contexts.
/// </remarks>
public sealed class GuardRailTests
{
    public sealed class RequiredMemberModel
    {
        [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        public required string Value { get; set; }
    }

    public sealed class ReadOnlyMemberModel
    {
        [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        public string Value => "read-only";
    }

    public sealed class ExtensionDataModel
    {
        [JsonExtensionData]
        [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        public Dictionary<string, JsonElement> Extra { get; set; } = [];
    }

    public sealed class ConflictingConditionsModel
    {
        [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)]
        public string? Value { get; set; } = "initial";
    }

    public sealed record ComplexParameterModel(
        [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] List<string> Values);

    public sealed record NumberHandlingParameterModel(
        [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] int Count = 3);

    public sealed record AlwaysOnParameterModel(
        [property: JsonIgnoreEx(JsonIgnoreConditionEx.Always)] string Name = "unknown");

    [Fact]
    public void A_required_member_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<RequiredMemberModel>("""{"Value":null}"""));

        Assert.Contains("required", error.Message);
        Assert.Contains("RequiredMemberModel.Value", error.Message);
    }

    [Fact]
    public void A_member_with_no_setter_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<ReadOnlyMemberModel>("""{"Value":null}"""));

        Assert.Contains("no setter", error.Message);
        Assert.Contains("ReadOnlyMemberModel.Value", error.Message);
    }

    [Fact]
    public void The_extension_data_member_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<ExtensionDataModel>("""{"anything":1}"""));

        Assert.Contains("extension data", error.Message);
    }

    [Fact]
    public void Two_conditions_on_the_same_side_are_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<ConflictingConditionsModel>("{}"));

        Assert.Contains("conflicting read-side", error.Message);
        Assert.Contains("WhenReadingNull", error.Message);
        Assert.Contains("WhenReadingDefault", error.Message);
    }

    [Fact]
    public void A_constructor_parameter_of_a_non_converter_type_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<ComplexParameterModel>("""{"Values":null}"""));

        Assert.Contains("single-token converter", error.Message);
    }

    [Fact]
    public void A_numeric_constructor_parameter_with_number_handling_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<NumberHandlingParameterModel>("""{"Count":null}"""));

        Assert.Contains("JsonNumberHandling", error.Message);
    }

    [Fact]
    public void Always_on_a_constructor_parameter_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<AlwaysOnParameterModel>("{}"));

        Assert.Contains("constructor parameter", error.Message);
    }

    [Fact]
    public void Skip_leaves_the_built_in_behaviour_in_place_instead_of_throwing()
    {
        JsonSerializerOptions options = Serializers.Create(
            SerializerConfiguration.Reflection,
            static settings => settings.UnsupportedMemberBehavior = UnsupportedMemberBehavior.Skip);

        RequiredMemberModel model = JsonSerializer.Deserialize<RequiredMemberModel>(
            """{"Value":null}""",
            options)!;

        Assert.Null(model.Value);
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Serializers.Create(SerializerConfiguration.Reflection))!;
}
