using System.Text.Json;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>
/// A member converter with HandleNull reads JSON null itself. The condition is about the incoming JSON null,
/// so it must be skipped before that converter sees it, on every kind of member, while the converter still
/// reads other tokens and writes exactly as it would without the library.
/// </summary>
public sealed class NullHandlingConverterTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_settable_member_skips_null_before_its_converter(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        NullHandlingConverterModel skipped = Serializers.Deserialize<NullHandlingConverterModel>(
            """{"Name":null,"Count":null}""", options);
        Assert.Equal("initial", skipped.Name);
        Assert.Equal(3, skipped.Count);

        NullHandlingConverterModel assigned = Serializers.Deserialize<NullHandlingConverterModel>(
            """{"Name":"x","Count":7}""", options);
        Assert.Equal("x", assigned.Name);
        Assert.Equal(7, assigned.Count);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_constructor_parameter_skips_null_before_its_converter(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.Equal("initial", Serializers.Deserialize<NullHandlingConverterRecord>("""{"Name":null}""", options).Name);
        Assert.Equal("x", Serializers.Deserialize<NullHandlingConverterRecord>("""{"Name":"x"}""", options).Name);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void The_converter_still_writes_null_its_own_way(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.Equal(
            """{"Name":"none","Count":-1}""",
            Serializers.Serialize(new NullHandlingConverterModel { Name = null, Count = null }, options));
        Assert.Equal(
            """{"Name":"none"}""",
            Serializers.Serialize(new NullHandlingConverterRecord(Name: null), options));
    }
}
