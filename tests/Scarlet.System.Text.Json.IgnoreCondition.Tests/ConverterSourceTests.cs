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

    /// <summary>A factory in the options is expanded first: its converter is what reads the null.</summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void An_options_converter_factory_whose_converter_reads_null_is_handled(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            configureOptions: static options => options.Converters.Add(new NullAsTextConverterFactory()));

        Assert.Equal("initial", Serializers.Deserialize<OptionsConverterModel>("""{"Name":null}""", options).Name);
        Assert.Equal("x", Serializers.Deserialize<OptionsConverterModel>("""{"Name":"x"}""", options).Name);
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
    /// A non-nullable value type is re-declared as T?, and a null can only be kept from the converter in
    /// effect if that converter can be lifted over T?, which needs it typed for exactly T. A base-type
    /// converter that reads null itself cannot be, so the condition is refused instead of letting it
    /// overwrite the initializer.
    /// </summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_base_type_options_converter_that_reads_null_is_refused_on_a_value_type(
        SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            configureOptions: static options => options.Converters.Add(new BaseTypeNullIntOptionsConverter()));

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Serializers.Deserialize<OptionsBaseNullConverterModel>("""{"Count":null}""", options));

        Assert.Contains("reads JSON null itself", error.Message);
        Assert.Contains(nameof(BaseTypeNullIntOptionsConverter), error.Message);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Skip_leaves_a_base_type_options_converter_to_the_serializer(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.UnsupportedMemberBehavior = UnsupportedMemberBehavior.Skip,
            static options => options.Converters.Add(new BaseTypeNullIntOptionsConverter()));

        Assert.Equal(-1, Serializers.Deserialize<OptionsBaseNullConverterModel>("""{"Count":null}""", options).Count);
        Assert.Equal(7, Serializers.Deserialize<OptionsBaseNullConverterModel>("""{"Count":7}""", options).Count);
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
