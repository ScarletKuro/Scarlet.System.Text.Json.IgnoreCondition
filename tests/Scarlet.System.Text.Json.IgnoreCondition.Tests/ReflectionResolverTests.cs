using System.Text.Json;
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
