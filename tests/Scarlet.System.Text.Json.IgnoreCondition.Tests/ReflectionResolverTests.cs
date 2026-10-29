using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>
/// The convenience constructors that resolve contracts by reflection. Everywhere else the tests pass a
/// resolver explicitly; these are the entry points the README's first example uses.
/// </summary>
/// <remarks>
/// Excluded from the NativeAOT build: the constructors are annotated RequiresDynamicCode.
/// </remarks>
public sealed class ReflectionResolverTests
{
    /// <summary>
    /// A JsonConverterAttribute subclass that creates its converter itself rather than naming a type. The
    /// source generator rejects these (SYSLIB1223), so only the reflection resolver can meet one.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TokenConverterAttribute : JsonConverterAttribute
    {
        public override JsonConverter CreateConverter(Type typeToConvert) => new AttributedTokenConverter();
    }

    [TokenConverter]
    public sealed record AttributedToken(string Text);

    /// <summary>Reads JSON null itself, as a sentinel token.</summary>
    public sealed class AttributedTokenConverter : JsonConverter<AttributedToken>
    {
        public override bool HandleNull => true;

        public override AttributedToken Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.TokenType == JsonTokenType.Null ? "from-null" : reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, AttributedToken value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Text);
    }

    public sealed class AttributedTokenModel
    {
        [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        public AttributedToken? Token { get; set; } = new("initial");
    }

    [Fact]
    public void A_type_attribute_that_creates_its_converter_is_handled()
    {
        JsonSerializerOptions options = Serializers.Create(SerializerConfiguration.Reflection);

        Assert.Equal("initial", Serializers.Deserialize<AttributedTokenModel>("""{"Token":null}""", options).Token?.Text);
        Assert.Equal("x", Serializers.Deserialize<AttributedTokenModel>("""{"Token":"x"}""", options).Token?.Text);
    }

    [Fact]
    public void The_parameterless_constructor_applies_the_conditions()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = new ExtendedIgnoreConditionResolver() };

        ValueModel? model = JsonSerializer.Deserialize<ValueModel>("""{"Age":null,"Season":null}""", options);

        Assert.NotNull(model);
        Assert.Equal(41, model.Age);
        Assert.Equal(Season.Summer, model.Season);
    }

    [Fact]
    public void The_settings_constructor_applies_the_settings()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new ExtendedIgnoreConditionResolver(
                new ExtendedIgnoreConditionSettings { DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull }),
        };

        OptionsDefaultModel? model = JsonSerializer.Deserialize<OptionsDefaultModel>("""{"Name":null}""", options);

        Assert.NotNull(model);
        Assert.Equal("initial", model.Name);
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ExtendedIgnoreConditionResolver(settings: null!));
        Assert.Throws<ArgumentNullException>(() => new ExtendedIgnoreConditionResolver(source: null!));
    }
}
