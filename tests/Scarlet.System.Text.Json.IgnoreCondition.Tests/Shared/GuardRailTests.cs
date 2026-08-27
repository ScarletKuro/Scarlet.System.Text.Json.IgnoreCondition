using System.Text.Json;
using System.Text.Json.Serialization;
using Scarlet.Text.Json.Serialization;
using Scarlet.Text.Json.Serialization.Metadata;
using Xunit;

namespace Scarlet.Text.Json.Tests;

/// <summary>
/// Cases where a read-side condition cannot be honoured. The compile-time analyzer rejects most of
/// these outright (SJIC001 to SJIC007), so the models here carry no attributes and the conditions are
/// registered by hand, which is also what an assembly that disables the generator would do.
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
        public required string Value { get; set; }
    }

    public sealed class ReadOnlyMemberModel
    {
        public string Value { get; } = "read-only";
    }

    public sealed class ExtensionDataModel
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement> Extra { get; set; } = [];
    }

    public sealed class MissingMemberModel
    {
        public string? Value { get; set; } = "initial";
    }

    public sealed class SkippedMemberModel
    {
        public required string Value { get; set; }
    }

    public sealed record ComplexParameterModel(List<string> Values);

    public sealed record NumberHandlingParameterModel(
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] int Count = 3);

    [Fact]
    public void A_required_member_is_refused()
    {
        ExtendedIgnoreConditionRegistry.Register(
            typeof(RequiredMemberModel),
            static builder => builder.Member<string>("Value", JsonIgnoreConditionEx.WhenReadingNull));

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<RequiredMemberModel>("""{"Value":null}"""));

        Assert.Contains("required", error.Message);
        Assert.Contains("RequiredMemberModel.Value", error.Message);
    }

    [Fact]
    public void A_member_with_no_setter_is_refused()
    {
        ExtendedIgnoreConditionRegistry.Register(
            typeof(ReadOnlyMemberModel),
            static builder => builder.Member<string>("Value", JsonIgnoreConditionEx.WhenReadingNull));

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<ReadOnlyMemberModel>("""{"Value":null}"""));

        Assert.Contains("no setter", error.Message);
    }

    [Fact]
    public void The_extension_data_member_is_refused()
    {
        ExtendedIgnoreConditionRegistry.Register(
            typeof(ExtensionDataModel),
            static builder => builder.Member<Dictionary<string, JsonElement>>(
                "Extra",
                JsonIgnoreConditionEx.WhenReadingNull));

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<ExtensionDataModel>("""{"anything":1}"""));

        Assert.Contains("extension data", error.Message);
    }

    [Fact]
    public void A_registration_that_matches_no_member_is_reported()
    {
        ExtendedIgnoreConditionRegistry.Register(
            typeof(MissingMemberModel),
            static builder => builder.Member<string>("Renamed", JsonIgnoreConditionEx.WhenReadingNull));

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<MissingMemberModel>("{}"));

        Assert.Contains("MissingMemberModel.Renamed", error.Message);
    }

    [Fact]
    public void A_constructor_parameter_of_a_non_converter_type_is_refused()
    {
        ExtendedIgnoreConditionRegistry.Register(
            typeof(ComplexParameterModel),
            static builder => builder.Member<List<string>>("Values", JsonIgnoreConditionEx.WhenReadingNull));

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<ComplexParameterModel>("""{"Values":null}"""));

        Assert.Contains("single-token converter", error.Message);
    }

    [Fact]
    public void A_numeric_constructor_parameter_with_number_handling_is_refused()
    {
        ExtendedIgnoreConditionRegistry.Register(
            typeof(NumberHandlingParameterModel),
            static builder => builder.ValueMember<int>("Count", JsonIgnoreConditionEx.WhenReadingNull));

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<NumberHandlingParameterModel>("""{"Count":null}"""));

        Assert.Contains("JsonNumberHandling", error.Message);
    }

    [Fact]
    public void Skip_leaves_the_built_in_behaviour_in_place_instead_of_throwing()
    {
        ExtendedIgnoreConditionRegistry.Register(
            typeof(SkippedMemberModel),
            static builder => builder.Member<string>("Value", JsonIgnoreConditionEx.WhenReadingNull));

        JsonSerializerOptions options = Serializers.Create(
            SerializerConfiguration.Reflection,
            static settings => settings.UnsupportedMemberBehavior = UnsupportedMemberBehavior.Skip);

        SkippedMemberModel model = JsonSerializer.Deserialize<SkippedMemberModel>(
            """{"Value":null}""",
            options)!;

        Assert.Null(model.Value);
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Serializers.Create(SerializerConfiguration.Reflection))!;
}
