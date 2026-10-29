using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>
/// The conditions have to coexist with the rest of the serializer's feature set, which is where a
/// mechanism that swapped in its own converter would show up.
/// </summary>
public sealed class InteropTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Read_conditions_combine_with_nullable_validation_and_write_defaults(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration, configureOptions: static o =>
        {
            o.RespectNullableAnnotations = true;
            o.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
        });

        NonNullableReferenceModel reference = Serializers.Deserialize<NonNullableReferenceModel>(
            """{"Name":null,"Values":null,"Field":null}""", options);
        Assert.Equal("initial", reference.Name);
        Assert.Equal(["initial"], reference.Values);
        Assert.Equal("initial", reference.Field);

        RecordModel record = Serializers.Deserialize<RecordModel>("""{"Name":null,"Age":null}""", options);
        Assert.Equal("unknown", record.Name);
        Assert.Equal(41, record.Age);

        ValueModel value = Serializers.Deserialize<ValueModel>("""{"Age":null,"Season":null}""", options);
        Assert.Equal(41, value.Age);
        Assert.Equal(Season.Summer, value.Season);
        value.Age = 0;
        value.Season = Season.Winter;
        using JsonDocument json = JsonDocument.Parse(Serializers.Serialize(value, options));
        Assert.False(json.RootElement.TryGetProperty("Age", out _));
        Assert.False(json.RootElement.TryGetProperty("Season", out _));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Projected_members_keep_options_level_write_defaults(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            configureOptions: static o => o.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault);

        using JsonDocument zero = JsonDocument.Parse(Serializers.Serialize(new ProjectedWriteDefaultsModel(), options));
        Assert.False(zero.RootElement.TryGetProperty("Count", out _));
        Assert.False(zero.RootElement.TryGetProperty("Enabled", out _));
        Assert.False(zero.RootElement.TryGetProperty("Id", out _));
        Assert.Equal(0, zero.RootElement.GetProperty("Optional").GetInt32());
        Assert.Equal(0, zero.RootElement.GetProperty("AlwaysWritten").GetInt32());

        var model = new ProjectedWriteDefaultsModel { Count = 9, Enabled = true, Id = ValueModel.KnownId };
        using JsonDocument nonzero = JsonDocument.Parse(Serializers.Serialize(model, options));
        Assert.Equal(9, nonzero.RootElement.GetProperty("Count").GetInt32());
        Assert.True(nonzero.RootElement.GetProperty("Enabled").GetBoolean());
        Assert.Equal(ValueModel.KnownId, nonzero.RootElement.GetProperty("Id").GetGuid());
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Projected_members_keep_custom_write_predicates(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.CreateUnmodified(configuration);
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
        IJsonTypeInfoResolver? baseResolver = options.TypeInfoResolver;
        Assert.NotNull(baseResolver);
        options.TypeInfoResolver = new ExtendedIgnoreConditionResolver(baseResolver.WithAddedModifier(static info =>
        {
            foreach (var property in info.Properties)
            {
                if (property.Name == "Count")
                {
                    property.ShouldSerialize = static (_, _) => true;
                }
            }
        }));

        using JsonDocument json = JsonDocument.Parse(Serializers.Serialize(new ProjectedWriteDefaultsModel(), options));
        Assert.Equal(0, json.RootElement.GetProperty("Count").GetInt32());
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Nullable_annotations_allow_skipped_nulls_but_still_validate_other_members(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            configureOptions: static o => o.RespectNullableAnnotations = true);

        NonNullableReferenceModel model = Serializers.Deserialize<NonNullableReferenceModel>(
            """{"Name":null,"Values":null,"Field":null}""", options);
        Assert.Equal("initial", model.Name);
        Assert.Equal(["initial"], model.Values);
        Assert.Equal("initial", model.Field);

        Assert.Throws<JsonException>(() => Serializers.Deserialize<NonNullableReferenceModel>(
            """{"Untouched":null}""", options));
        Assert.Throws<JsonException>(() => Serializers.Serialize(
            new NonNullableReferenceModel { Name = null! }, options));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Polymorphic_reads_keep_the_discriminator_and_the_conditions(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        var cat = (CatShape)Serializers.Deserialize<AnimalShape>(
            """{"$kind":"cat","Name":null,"Age":null,"Indoor":null}""",
            options);

        Assert.Equal("initial", cat.Name);
        Assert.Equal(41, cat.Age);
        Assert.True(cat.Indoor);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Polymorphic_writes_are_unchanged(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        string json = Serializers.Serialize<AnimalShape>(new DogShape { Name = "rex", Age = 3 }, options);

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

        string json = Serializers.Serialize(node, options);
        GraphNode round = Serializers.Deserialize<GraphNode>(json, options);

        Assert.Equal("first", round.Name);
        Assert.Same(round, round.Next);

        GraphNode skipped = Serializers.Deserialize<GraphNode>("""{"$id":"1","Name":null}""", options);
        Assert.Equal("initial", skipped.Name);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Populate_handling_is_preserved(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        PopulateModel populated = Serializers.Deserialize<PopulateModel>("""{"Values":["b"]}""", options);
        Assert.Equal(["a", "b"], populated.Values);

        PopulateModel skipped = Serializers.Deserialize<PopulateModel>("""{"Values":null}""", options);
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

        NumberHandlingModel fromStrings = Serializers.Deserialize<NumberHandlingModel>(
            """{"Count":"9","Ratio":"2.5"}""",
            options);

        Assert.Equal(9, fromStrings.Count);
        Assert.Equal(2.5, fromStrings.Ratio);

        NumberHandlingModel skipped = Serializers.Deserialize<NumberHandlingModel>(
            """{"Count":null,"Ratio":null}""",
            options);

        Assert.Equal(3, skipped.Count);
        Assert.Equal(1.5, skipped.Ratio);

        Assert.Contains("\"Ratio\":\"1.5\"", Serializers.Serialize(new NumberHandlingModel(), options));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Unmapped_member_handling_still_sees_unknown_members(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            configureOptions: static o => o.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);

        Assert.Throws<JsonException>(() =>
            Serializers.Deserialize<ReferenceModel>("""{"Nope":1}""", options));

        ReferenceModel model = Serializers.Deserialize<ReferenceModel>("""{"Name":null}""", options);
        Assert.Equal("initial", model.Name);
    }
}
