using System.Text.Json;
using Xunit;

namespace Scarlet.Text.Json.Tests;

/// <summary>
/// Members bound to a deserialization constructor parameter never go through a setter, so the
/// condition substitutes the parameter's own default value for an incoming null.
/// </summary>
public sealed class ConstructorConditionTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Null_falls_back_to_the_parameter_default(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        RecordModel model = Deserialize<RecordModel>(
            """{"Name":null,"Age":null,"Nickname":null}""",
            options);

        Assert.Equal("unknown", model.Name);
        Assert.Equal(41, model.Age);
        Assert.Null(model.Nickname);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Non_null_values_are_still_used(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        RecordModel model = Deserialize<RecordModel>(
            """{"Name":"given","Age":9,"Nickname":"nick"}""",
            options);

        Assert.Equal("given", model.Name);
        Assert.Equal(9, model.Age);
        Assert.Equal("nick", model.Nickname);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Absent_members_still_use_the_parameter_default(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        RecordModel model = Deserialize<RecordModel>("{}", options);

        Assert.Equal("unknown", model.Name);
        Assert.Equal(41, model.Age);
    }

    /// <summary>
    /// The attribute written straight on a positional parameter, which is where the compiler puts it
    /// without a <c>property:</c> target.
    /// </summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_condition_on_the_parameter_itself_is_honoured(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        ParameterRecordModel model = Deserialize<ParameterRecordModel>("""{"Name":null,"Age":null}""", options);

        Assert.Equal("unknown", model.Name);
        Assert.Equal(41, model.Age);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_parameter_without_a_default_falls_back_to_the_type_default(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        RequiredParameterRecordModel model = Deserialize<RequiredParameterRecordModel>("""{"Name":null}""", options);

        Assert.Null(model.Name);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Records_still_serialize_normally(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        string json = JsonSerializer.Serialize(new RecordModel("given", 9, "nick"), options);

        Assert.Equal("""{"Name":"given","Age":9,"Nickname":"nick"}""", json);
    }

    private static T Deserialize<T>(string json, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<T>(json, options)!;
}
