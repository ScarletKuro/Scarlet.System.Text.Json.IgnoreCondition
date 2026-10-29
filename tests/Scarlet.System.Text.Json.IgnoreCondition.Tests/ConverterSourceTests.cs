using System.Text.Json;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>
/// Converters that do not sit on the member: registered in the options, or declared on the member's type.
/// The serializer uses them all the same, so the condition has to hold against them as it does against a
/// member converter: a JSON null is skipped before a converter that reads null itself, and a converter for
/// a base type is honoured.
/// </summary>
public sealed class ConverterSourceTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    /// <summary>Covers the setter path (string, int?) and the T? re-declaration path (int).</summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void Options_converters_that_read_null_do_not_overwrite_initializers(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration, configureOptions: static options =>
        {
            options.Converters.Add(new NullAsTextConverter());
            options.Converters.Add(new NullAsMinusOneConverter());
            options.Converters.Add(new NullAsMinusHundredConverter());
        });

        OptionsConverterModel skipped = Serializers.Deserialize<OptionsConverterModel>(
            """{"Name":null,"Optional":null,"Count":null}""", options);
        Assert.Equal("initial", skipped.Name);
        Assert.Equal(3, skipped.Optional);
        Assert.Equal(5, skipped.Count);

        OptionsConverterModel assigned = Serializers.Deserialize<OptionsConverterModel>(
            """{"Name":"x","Optional":7,"Count":8}""", options);
        Assert.Equal("x", assigned.Name);
        Assert.Equal(7, assigned.Optional);
        Assert.Equal(8, assigned.Count);

        // Writing a null still goes through the options converter, exactly as without the library.
        Assert.Equal(
            """{"Name":"none","Optional":-1,"Count":5}""",
            Serializers.Serialize(new OptionsConverterModel { Name = null, Optional = null }, options));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_type_level_converter_that_reads_null_does_not_overwrite_the_initializer(
        SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.Equal("initial", Serializers.Deserialize<TypeLevelConverterModel>("""{"Token":null}""", options).Token?.Text);
        Assert.Equal("x", Serializers.Deserialize<TypeLevelConverterModel>("""{"Token":"x"}""", options).Token?.Text);
    }

    /// <summary>
    /// The serializer adapts a converter for a base type internally. A constructor parameter's substituting
    /// converter used to cast it to the member type and fail with an InvalidCastException.
    /// </summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_base_type_options_converter_works_with_constructor_parameters(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            configureOptions: static options => options.Converters.Add(new BaseTypeOptionsConverter()));

        Assert.Equal(7, Serializers.Deserialize<OptionsBaseConverterRecord>("""{"Count":7}""", options).Count);
        Assert.Equal(9, Serializers.Deserialize<OptionsBaseConverterRecord>("""{"Count":null}""", options).Count);
        Assert.Equal("""{"Count":7}""", Serializers.Serialize(new OptionsBaseConverterRecord(7), options));
    }
}
