using System.Text.Json;
using Scarlet.System.Text.Json.IgnoreCondition.Internal;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

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
    public void Closed_array_arguments_are_registered_and_round_trip(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration,
            static settings => settings.RequireValueTypeRegistration = true);

        ArrayArgumentModel skipped = Serializers.Deserialize<ArrayArgumentModel>("""{"Box":null}""", options);
        Assert.Equal([1, 2], skipped.Box.Value);

        const string json = """{"Box":{"Value":[3,4]}}""";
        ArrayArgumentModel assigned = Serializers.Deserialize<ArrayArgumentModel>(json, options);
        Assert.Equal([3, 4], assigned.Box.Value);
        Assert.Equal(json, Serializers.Serialize(assigned, options));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_struct_with_members_keeps_its_initial_value(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        CustomValueTypeModel model = Serializers.Deserialize<CustomValueTypeModel>(
            """{"Location":null,"Priority":null}""",
            options);

        Assert.Equal(new Point(3, 4), model.Location);
        Assert.Equal(Priority.High, model.Priority);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_struct_with_members_still_round_trips(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        CustomValueTypeModel model = Serializers.Deserialize<CustomValueTypeModel>(
            """{"Location":{"X":7,"Y":8},"Priority":0}""",
            options);

        Assert.Equal(new Point(7, 8), model.Location);
        Assert.Equal(Priority.Low, model.Priority);

        string json = Serializers.Serialize(model, options);
        Assert.Contains("""
            "Location":{"X":7,"Y":8}
            """, json);
    }

    /// <summary>
    /// Registration is what makes these types work where generic instantiations cannot be created at
    /// run time. It has to be idempotent: the generator and a consumer may both register a type.
    /// </summary>
    [Fact]
    public void Registering_a_value_type_is_idempotent()
    {
        ExtendedIgnoreConditionResolver.RegisterValueType<ManualOnly>();
        ExtendedIgnoreConditionResolver.RegisterValueType<ManualOnly>();

        Assert.NotNull(MemberOps.Resolve(typeof(ManualOnly), requireRegistration: true));
        Assert.NotNull(MemberOps.Resolve(typeof(ManualOnly?), requireRegistration: true));
    }

    /// <summary>
    /// Nothing in the test assembly calls RegisterValueType for these: the generated module initializer
    /// does. A lookup that demands registration proves it, even under JIT.
    /// </summary>
    [Fact]
    public void The_generator_registers_custom_value_types_of_annotated_members()
    {
        Assert.NotNull(MemberOps.Resolve(typeof(Season), requireRegistration: true));
        Assert.NotNull(MemberOps.Resolve(typeof(Priority), requireRegistration: true));
        Assert.NotNull(MemberOps.Resolve(typeof(Point), requireRegistration: true));
        Assert.NotNull(MemberOps.Resolve(typeof(Point?), requireRegistration: true));

        // Reached by no [JsonIgnoreEx] member, so not generated.
        Assert.Null(MemberOps.Resolve(typeof(Shade), requireRegistration: true));
    }
}
