using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>
/// Members bound to a deserialization constructor parameter never go through a setter, so the
/// condition substitutes the parameter's own default value for an incoming null.
/// </summary>
public sealed class ConstructorConditionTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Object_parameters_are_refused_on_both_read_and_write(SerializerConfiguration configuration)
    {
        Assert.Throws<NotSupportedException>(() => Serializers.Serialize(
            new ObjectPayloadModel(1, 2), Serializers.Create(configuration)));
        Assert.Throws<NotSupportedException>(() => Serializers.Deserialize<ObjectPayloadModel>(
            """{"First":1,"Payload":null}""", Serializers.Create(configuration)));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Skipping_an_unsupported_object_condition_preserves_native_behavior(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration,
            static settings => settings.UnsupportedMemberBehavior = UnsupportedMemberBehavior.Skip,
            static options => options.ReferenceHandler = ReferenceHandler.Preserve);

        using JsonDocument json = JsonDocument.Parse(Serializers.Serialize(new ObjectPayloadModel(1, 2), options));
        Assert.Equal(2, json.RootElement.GetProperty("Payload").GetInt32());

        ObjectPayloadModel model = Serializers.Deserialize<ObjectPayloadModel>(
            """{"First":{"$id":"1","Value":2},"Payload":{"$ref":"1"}}""", options);
        Assert.Same(model.First, model.Payload);
    }

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
    public void A_member_renamed_to_a_parameter_name_is_not_treated_as_constructor_bound(
        SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        JsonNameCollisionModel model = Deserialize<JsonNameCollisionModel>(
            """{"heading":"given","title":null}""",
            options);

        Assert.Equal("given", model.Title);
        Assert.Equal("initial", model.Subtitle);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void WhenReadingDefault_also_replaces_the_type_default_with_the_parameter_default(
        SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.Equal(41, Deserialize<DefaultParameterRecord>("""{"Age":null}""", options).Age);
        Assert.Equal(41, Deserialize<DefaultParameterRecord>("""{"Age":0}""", options).Age);
        Assert.Equal(5, Deserialize<DefaultParameterRecord>("""{"Age":5}""", options).Age);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void An_enum_parameter_falls_back_to_its_default(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.Equal(Season.Summer, Deserialize<EnumParameterRecord>("""{"Season":null}""", options).Season);
        Assert.Equal(Season.Winter, Deserialize<EnumParameterRecord>("""{"Season":0}""", options).Season);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_nullable_enum_parameter_falls_back_to_its_default(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.Equal(Season.Autumn, Serializers.Deserialize<NullableEnumParameterRecord>("""{"Season":null}""", options).Season);
        Assert.Equal(Season.Autumn, Serializers.Deserialize<NullableEnumParameterRecord>("{}", options).Season);
        Assert.Equal(Season.Spring, Serializers.Deserialize<NullableEnumParameterRecord>("""{"Season":1}""", options).Season);
    }

    /// <summary>The same parameter reached only by an options-level default, which used to break the whole type.</summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_default_read_condition_handles_a_nullable_enum_parameter(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull);

        Assert.Equal(Season.Autumn, Serializers.Deserialize<UnannotatedNullableEnumRecord>("{}", options).Season);
        Assert.Equal(Season.Autumn, Serializers.Deserialize<UnannotatedNullableEnumRecord>("""{"Season":null}""", options).Season);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_null_constructor_bound_member_is_still_written(SerializerConfiguration configuration)
    {
        string json = Serializers.Serialize(new RecordModel("given", 9, Nickname: null), Serializers.Create(configuration));

        Assert.Equal("""{"Name":"given","Age":9,"Nickname":null}""", json);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Records_still_serialize_normally(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        string json = Serializers.Serialize(new RecordModel("given", 9, "nick"), options);

        Assert.Equal("""{"Name":"given","Age":9,"Nickname":"nick"}""", json);
    }

    private static T Deserialize<T>(string json, JsonSerializerOptions options) =>
        Serializers.Deserialize<T>(json, options);
}
