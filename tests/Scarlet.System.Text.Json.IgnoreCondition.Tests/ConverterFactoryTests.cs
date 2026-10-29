using System.Text.Json;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>
/// A member-level <c>[JsonConverter]</c> naming a converter factory. Both the constructor-parameter and the
/// nullable-projection mechanisms wrap the member's converter, so they need the converter the factory
/// creates, not the factory itself, and must behave the same in every resolver mode.
/// </summary>
public sealed class ConverterFactoryTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_settable_member_with_a_converter_factory_skips_null_and_keeps_the_converter(
        SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.Equal(Season.Autumn, Serializers.Deserialize<ConverterFactoryModel>("""{"Season":null}""", options)!.Season);
        Assert.Equal(Season.Spring, Serializers.Deserialize<ConverterFactoryModel>("""{"Season":"Spring"}""", options)!.Season);
        Assert.Equal("""{"Season":"Autumn"}""", Serializers.Serialize(new ConverterFactoryModel(), options));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_constructor_parameter_with_a_converter_factory_skips_null_and_keeps_the_converter(
        SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.Equal(Season.Autumn, Serializers.Deserialize<ConverterFactoryRecord>("""{"Season":null}""", options)!.Season);
        Assert.Equal(Season.Spring, Serializers.Deserialize<ConverterFactoryRecord>("""{"Season":"Spring"}""", options)!.Season);
        Assert.Equal("""{"Season":"Autumn"}""", Serializers.Serialize(new ConverterFactoryRecord(), options));
    }
}
