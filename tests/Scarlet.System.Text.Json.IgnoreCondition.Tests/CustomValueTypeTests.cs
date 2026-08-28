using System.Text.Json;
using Scarlet.Text.Json.Serialization;
using Xunit;

namespace Scarlet.Text.Json.Tests;

/// <summary>
/// Value types the library does not root itself: a struct with members of its own, and a user enum.
/// Both reach the same nullable projection, so the serializer's own <c>Nullable&lt;T&gt;</c> converter
/// has to carry a non-primitive element type correctly.
/// </summary>
public sealed class CustomValueTypeTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_struct_with_members_keeps_its_initial_value(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        CustomValueTypeModel model = JsonSerializer.Deserialize<CustomValueTypeModel>(
            """{"Location":null,"Priority":null}""",
            options)!;

        Assert.Equal(new Point(3, 4), model.Location);
        Assert.Equal(Priority.High, model.Priority);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_struct_with_members_still_round_trips(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        CustomValueTypeModel model = JsonSerializer.Deserialize<CustomValueTypeModel>(
            """{"Location":{"X":7,"Y":8},"Priority":0}""",
            options)!;

        Assert.Equal(new Point(7, 8), model.Location);
        Assert.Equal(Priority.Low, model.Priority);

        string json = JsonSerializer.Serialize(model, options);
        Assert.Contains("""
            "Location":{"X":7,"Y":8}
            """, json);
    }

    /// <summary>
    /// Registration is what makes these types work where generic instantiations cannot be created at
    /// run time. It has to be idempotent, because it is a static call consumers make at startup.
    /// </summary>
    [Fact]
    public void Registering_a_value_type_is_idempotent()
    {
        ExtendedIgnoreConditionResolver.RegisterValueType<Point>();
        ExtendedIgnoreConditionResolver.RegisterValueType<Point>();
        ExtendedIgnoreConditionResolver.RegisterValueType<Priority>();

        JsonSerializerOptions options = Serializers.Create(SerializerConfiguration.Reflection);

        CustomValueTypeModel model = JsonSerializer.Deserialize<CustomValueTypeModel>(
            """{"Location":null,"Priority":null}""",
            options)!;

        Assert.Equal(new Point(3, 4), model.Location);
        Assert.Equal(Priority.High, model.Priority);
    }
}
