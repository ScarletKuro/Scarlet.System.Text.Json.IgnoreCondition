using System.Text.Json;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

public sealed class GenericAndIgnoredMemberTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_generic_model_is_supported(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        GenericModel<string> model = JsonSerializer.Deserialize<GenericModel<string>>(
            """{"Value":null,"Count":null}""",
            options)!;

        Assert.Null(model.Value);
        Assert.Equal(5, model.Count);

        GenericModel<string> assigned = JsonSerializer.Deserialize<GenericModel<string>>(
            """{"Value":"x","Count":9}""",
            options)!;

        Assert.Equal("x", assigned.Value);
        Assert.Equal(9, assigned.Count);
    }

    /// <summary>
    /// [JsonIgnore] wins because it removes the member from the contract before the resolver runs, so
    /// the extended condition has nothing to act on and must not throw over it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void JsonIgnore_on_the_same_member_is_not_an_error(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        IgnoredMemberModel model = JsonSerializer.Deserialize<IgnoredMemberModel>(
            """{"Hidden":"from json","Visible":null}""",
            options)!;

        Assert.Equal("initial", model.Hidden);
        Assert.Equal("initial", model.Visible);
        Assert.DoesNotContain("Hidden", JsonSerializer.Serialize(model, options));
    }
}
