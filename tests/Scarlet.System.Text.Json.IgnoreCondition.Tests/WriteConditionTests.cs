using System.Text.Json;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

public sealed class WriteConditionTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void WhenWritingNull_omits_only_nulls(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.DoesNotContain("WhenWritingNull", Serialize(new WriteModel(), options));
        Assert.Contains("""
            "WhenWritingNull":"x"
            """, Serialize(new WriteModel { WhenWritingNull = "x" }, options));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void WhenWritingDefault_omits_the_type_default(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.DoesNotContain("WhenWritingDefault", Serialize(new WriteModel(), options));
        Assert.Contains("\"WhenWritingDefault\":1", Serialize(new WriteModel { WhenWritingDefault = 1 }, options));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void WhenWriting_never_serializes_but_still_deserializes(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.DoesNotContain("WhenWriting\"", Serialize(new WriteModel { WhenWriting = "x" }, options));

        WriteModel model = Deserialize<WriteModel>("""{"WhenWriting":"from json"}""", options);
        Assert.Equal("from json", model.WhenWriting);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Always_removes_the_member_from_the_contract(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.DoesNotContain("Always", Serialize(new WriteModel(), options));

        WriteModel model = Deserialize<WriteModel>("""{"Always":"from json"}""", options);
        Assert.Equal("always", model.Always);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Members_without_a_condition_are_still_written(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.Contains("""
            "Kept":"kept"
            """, Serialize(new WriteModel(), options));
    }

    private static T Deserialize<T>(string json, JsonSerializerOptions options) =>
        Serializers.Deserialize<T>(json, options)!;

    private static string Serialize<T>(T value, JsonSerializerOptions options) =>
        Serializers.Serialize(value, options);
}
