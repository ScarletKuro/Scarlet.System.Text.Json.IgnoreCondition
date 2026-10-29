using System.Text.Json;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>
/// Init-only members behave differently per resolver. The reflection resolver calls the init accessor like
/// any setter, so the condition applies. The source generator assigns init-only members through an object
/// initializer: the setter is never called, and an absent member loses its initializer even without this
/// library. An explicit condition there is refused rather than silently ignored.
/// </summary>
public sealed class InitOnlyTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void An_explicit_condition_applies_under_reflection_and_is_refused_under_source_generation(
        SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        if (configuration == SerializerConfiguration.Reflection)
        {
            InitOnlyModel skipped = Serializers.Deserialize<InitOnlyModel>("""{"Name":null,"Count":null}""", options);
            Assert.Equal("initial", skipped.Name);
            Assert.Equal(3, skipped.Count);

            InitOnlyModel assigned = Serializers.Deserialize<InitOnlyModel>("""{"Name":"x","Count":7}""", options);
            Assert.Equal("x", assigned.Name);
            Assert.Equal(7, assigned.Count);
        }
        else
        {
            NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
                Serializers.Deserialize<InitOnlyModel>("""{"Name":"x"}""", options));

            Assert.Contains("init-only", error.Message);
            Assert.Contains("InitOnlyModel", error.Message);
        }
    }

    /// <summary>An options-level default is best-effort: under source generation it leaves init-only members native.</summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void An_options_level_default_does_not_throw_on_init_only_members(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull);

        UnannotatedInitOnlyModel model = Serializers.Deserialize<UnannotatedInitOnlyModel>("""{"Name":null}""", options);

        Assert.Equal(configuration == SerializerConfiguration.Reflection ? "initial" : null, model.Name);
    }
}
