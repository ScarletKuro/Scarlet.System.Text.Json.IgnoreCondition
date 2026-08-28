using System.Text.Json;
using Scarlet.Text.Json.Serialization;
using Xunit;

namespace Scarlet.Text.Json.Tests;

/// <summary>
/// The options-level default, which is the replacement for the read-side half of the obsolete
/// <c>JsonSerializerOptions.IgnoreNullValues</c> (dotnet/runtime#62086).
/// </summary>
public sealed class OptionsDefaultTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void DefaultReadCondition_applies_to_members_with_no_attribute(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull);

        OptionsDefaultModel model = JsonSerializer.Deserialize<OptionsDefaultModel>(
            """{"Name":null,"Values":null,"Optional":null}""",
            options)!;

        Assert.Equal("initial", model.Name);
        Assert.Equal(["a"], model.Values);
        Assert.Equal(7, model.Optional);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Never_opts_a_single_member_out_of_the_default(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull);

        OptionsDefaultModel model = JsonSerializer.Deserialize<OptionsDefaultModel>(
            """{"Name":null,"OptedOut":null}""",
            options)!;

        Assert.Equal("initial", model.Name);
        Assert.Null(model.OptedOut);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void DefaultWriteCondition_applies_to_members_with_no_attribute(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.DefaultWriteCondition = JsonIgnoreConditionEx.WhenWritingNull);

        string json = JsonSerializer.Serialize(
            new OptionsDefaultModel { Name = null, Optional = null, OptedOut = null },
            options);

        Assert.DoesNotContain("\"Name\"", json);
        Assert.DoesNotContain("\"Optional\"", json);
        Assert.Contains("\"OptedOut\":null", json);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_default_does_not_break_members_it_cannot_apply_to(SerializerConfiguration configuration)
    {
        // WriteModel has read-only-ish and constructor-free members plus explicit conditions; a
        // sweeping default must not throw for any of them.
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull);

        WriteModel model = JsonSerializer.Deserialize<WriteModel>("""{"Kept":null}""", options)!;

        Assert.Equal("kept", model.Kept);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_default_read_condition_reaches_constructor_parameters(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull);

        RecordModel model = JsonSerializer.Deserialize<RecordModel>("""{"Name":null,"Age":null}""", options)!;

        Assert.Equal("unknown", model.Name);
        Assert.Equal(41, model.Age);
    }

    [Fact]
    public void A_write_condition_cannot_be_used_as_the_read_default()
    {
        var settings = new ExtendedIgnoreConditionSettings();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenWritingNull);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            settings.DefaultWriteCondition = JsonIgnoreConditionEx.WhenReadingNull);
    }
}
