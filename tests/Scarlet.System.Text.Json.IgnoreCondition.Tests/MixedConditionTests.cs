using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

public sealed class MixedConditionTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Native_write_and_extended_read_rules_combine(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration,
            configureOptions: static o => o.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
        MixedConditionModel model = Serializers.Deserialize<MixedConditionModel>(
            """{"Name":null,"Count":null,"ReadIgnored":"incoming"}""", options)!;
        Assert.Equal("initial", model.Name);
        Assert.Equal(7, model.Count);
        Assert.Equal("initial", model.ReadIgnored);

        model.Name = null;
        model.Count = 0;
        model.ReadIgnored = null;
        model.AlwaysWritten = 0;
        using JsonDocument json = JsonDocument.Parse(Serializers.Serialize(model, options));
        Assert.False(json.RootElement.TryGetProperty("Name", out _));
        Assert.False(json.RootElement.TryGetProperty("Count", out _));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("ReadIgnored").ValueKind);
        Assert.Equal(0, json.RootElement.GetProperty("AlwaysWritten").GetInt32());
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Explicit_native_rules_take_precedence_over_extended_read_defaults(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration, static settings =>
            settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull);
        NativePrecedenceModel model = Serializers.Deserialize<NativePrecedenceModel>(
            """{"NeverRead":"incoming"}""", options)!;
        Assert.Equal("initial", model.NeverRead);
        using JsonDocument json = JsonDocument.Parse(Serializers.Serialize(model, options));
        Assert.False(json.RootElement.TryGetProperty("NeverWritten", out _));
        Assert.False(json.RootElement.TryGetProperty("Count", out _));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Explicit_extended_rules_apply_even_with_native_Never(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);
        MixedConditionModel model = Serializers.Deserialize<MixedConditionModel>("""{"AlwaysWritten":null}""", options)!;
        Assert.Equal(9, model.AlwaysWritten);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Conflicting_native_read_rules_are_refused_before_setter_validation(SerializerConfiguration configuration)
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Serializers.Deserialize<ConflictingNativeReadModel>("{}", Serializers.Create(configuration)));
        Assert.Contains("ConflictingNativeReadModel.Value", error.Message);
        Assert.Contains("conflicting read-side", error.Message);
        Assert.Contains("WhenReadingNull", error.Message);
        Assert.Contains("WhenReading", error.Message);
    }

    /// <summary>A bare attribute would read like native [JsonIgnore]; it must not compile.</summary>
    [Fact]
    public void The_attribute_has_no_parameterless_constructor() =>
        Assert.DoesNotContain(
            typeof(JsonIgnoreExAttribute).GetConstructors(),
            static constructor => constructor.GetParameters().Length == 0);
}
