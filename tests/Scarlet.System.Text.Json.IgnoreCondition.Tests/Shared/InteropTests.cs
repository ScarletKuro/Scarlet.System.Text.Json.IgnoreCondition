using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Scarlet.Text.Json.Tests;

/// <summary>
/// The conditions have to coexist with the rest of the serializer's feature set, which is where a
/// mechanism that swapped in its own converter would show up.
/// </summary>
public sealed class InteropTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Polymorphic_reads_keep_the_discriminator_and_the_conditions(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        var cat = (CatShape)JsonSerializer.Deserialize<AnimalShape>(
            """{"$kind":"cat","Name":null,"Age":null,"Indoor":null}""",
            options)!;

        Assert.Equal("initial", cat.Name);
        Assert.Equal(41, cat.Age);
        Assert.True(cat.Indoor);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Polymorphic_writes_are_unchanged(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        string json = JsonSerializer.Serialize<AnimalShape>(new DogShape { Name = "rex", Age = 3 }, options);

        Assert.Contains("\"$kind\":\"dog\"", json);
        Assert.Contains("\"Name\":\"rex\"", json);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Reference_preservation_still_works(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            configureOptions: static o => o.ReferenceHandler = ReferenceHandler.Preserve);

        var node = new GraphNode { Name = "first" };
        node.Next = node;

        string json = JsonSerializer.Serialize(node, options);
        GraphNode round = JsonSerializer.Deserialize<GraphNode>(json, options)!;

        Assert.Equal("first", round.Name);
        Assert.Same(round, round.Next);

        GraphNode skipped = JsonSerializer.Deserialize<GraphNode>("""{"$id":"1","Name":null}""", options)!;
        Assert.Equal("initial", skipped.Name);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Populate_handling_is_preserved(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        PopulateModel populated = JsonSerializer.Deserialize<PopulateModel>("""{"Values":["b"]}""", options)!;
        Assert.Equal(["a", "b"], populated.Values);

        PopulateModel skipped = JsonSerializer.Deserialize<PopulateModel>("""{"Values":null}""", options)!;
        Assert.Equal(["a"], skipped.Values);
    }

    /// <summary>
    /// The nullable projection reuses the serializer's own <c>Nullable&lt;T&gt;</c> converter, so
    /// number handling configured for the member keeps working.
    /// </summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void Number_handling_on_a_projected_member_is_preserved(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        NumberHandlingModel fromStrings = JsonSerializer.Deserialize<NumberHandlingModel>(
            """{"Count":"9","Ratio":"2.5"}""",
            options)!;

        Assert.Equal(9, fromStrings.Count);
        Assert.Equal(2.5, fromStrings.Ratio);

        NumberHandlingModel skipped = JsonSerializer.Deserialize<NumberHandlingModel>(
            """{"Count":null,"Ratio":null}""",
            options)!;

        Assert.Equal(3, skipped.Count);
        Assert.Equal(1.5, skipped.Ratio);

        Assert.Contains("\"Ratio\":\"1.5\"", JsonSerializer.Serialize(new NumberHandlingModel(), options));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Unmapped_member_handling_still_sees_unknown_members(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            configureOptions: static o => o.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ReferenceModel>("""{"Nope":1}""", options));

        ReferenceModel model = JsonSerializer.Deserialize<ReferenceModel>("""{"Name":null}""", options)!;
        Assert.Equal("initial", model.Name);
    }
}
