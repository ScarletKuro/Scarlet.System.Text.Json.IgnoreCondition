using System.Text.Json;
using Xunit;

namespace Scarlet.Text.Json.Tests;

/// <summary>
/// The behaviour the library exists for: an incoming JSON null leaves the member at the value the
/// object was constructed with.
/// </summary>
public sealed class ReadConditionTests
{
    public static TheoryData<SerializerConfiguration> Configurations =>
    [
        SerializerConfiguration.Reflection,
        SerializerConfiguration.SourceGenDefault,
        SerializerConfiguration.SourceGenMetadata,
    ];

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Null_leaves_reference_members_at_their_initial_value(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        ReferenceModel model = Deserialize<ReferenceModel>(
            """{"Name":null,"Values":null,"Optional":null}""",
            options);

        Assert.Equal("initial", model.Name);
        Assert.Equal(["a"], model.Values);
        Assert.Equal(7, model.Optional);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Non_null_values_are_still_assigned(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        ReferenceModel model = Deserialize<ReferenceModel>(
            """{"Name":"1234","Values":["b","c"],"Optional":9}""",
            options);

        Assert.Equal("1234", model.Name);
        Assert.Equal(["b", "c"], model.Values);
        Assert.Equal(9, model.Optional);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Absent_members_are_untouched(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        ReferenceModel model = Deserialize<ReferenceModel>("{}", options);

        Assert.Equal("initial", model.Name);
        Assert.Equal(["a"], model.Values);
        Assert.Equal(7, model.Optional);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Members_without_a_condition_keep_the_built_in_behaviour(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        ReferenceModel model = Deserialize<ReferenceModel>("""{"Untouched":null}""", options);

        Assert.Null(model.Untouched);
    }

    /// <summary>dotnet/runtime#74008 and #83706: this throws without the library.</summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void Null_into_a_non_nullable_value_type_is_skipped_instead_of_throwing(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        ValueModel model = Deserialize<ValueModel>(
            """{"Age":null,"Id":null,"Timestamp":null,"Season":null,"Amount":null}""",
            options);

        Assert.Equal(41, model.Age);
        Assert.Equal(ValueModel.KnownId, model.Id);
        Assert.Equal(ValueModel.KnownTimestamp, model.Timestamp);
        Assert.Equal(Season.Summer, model.Season);
        Assert.Equal(12.5m, model.Amount);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Value_type_members_still_round_trip_real_values(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);
        var expectedId = new Guid("11112222-3333-4444-5555-666677778888");

        ValueModel model = Deserialize<ValueModel>(
            $$"""{"Age":7,"Id":"{{expectedId}}","Timestamp":"2020-01-02T03:04:05Z","Season":1,"Amount":0.25}""",
            options);

        Assert.Equal(7, model.Age);
        Assert.Equal(expectedId, model.Id);
        Assert.Equal(new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc), model.Timestamp.ToUniversalTime());
        Assert.Equal(Season.Spring, model.Season);
        Assert.Equal(0.25m, model.Amount);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Value_type_members_still_serialize_as_bare_values(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        string json = Serialize(new ValueModel { Age = 5 }, options);

        Assert.Contains("\"Age\":5", json);
        Assert.DoesNotContain("null", json);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Fields_are_supported(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        FieldModel model = Deserialize<FieldModel>("""{"Name":null,"Age":null}""", options);

        Assert.Equal("initial", model.Name);
        Assert.Equal(41, model.Age);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Struct_targets_are_supported(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        StructModel model = Deserialize<StructModel>("""{"Name":null,"Age":null}""", options);

        Assert.Equal("initial", model.Name);
        Assert.Equal(41, model.Age);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Renamed_members_are_matched(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        RenamedModel model = Deserialize<RenamedModel>("""{"display_name":null,"RetryCount":null}""", options);

        Assert.Equal("initial", model.Name);
        Assert.Equal(3, model.RetryCount);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_naming_policy_does_not_break_matching(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            configureOptions: static o => o.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        RenamedModel model = Deserialize<RenamedModel>("""{"display_name":null,"retry_count":null}""", options);

        Assert.Equal("initial", model.Name);
        Assert.Equal(3, model.RetryCount);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Case_insensitive_matching_is_honoured(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            configureOptions: static o => o.PropertyNameCaseInsensitive = true);

        ValueModel model = Deserialize<ValueModel>("""{"age":null}""", options);

        Assert.Equal(41, model.Age);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Conditions_on_a_base_class_apply_to_a_derived_contract(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        DerivedModel model = Deserialize<DerivedModel>("""{"Name":null,"Extra":null}""", options);

        Assert.Equal("initial", model.Name);
        Assert.Null(model.Extra);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void WhenReading_ignores_the_value_but_keeps_serializing(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        WriteModel model = Deserialize<WriteModel>("""{"WhenReading":"from json"}""", options);
        Assert.Equal("read", model.WhenReading);

        Assert.Contains("\"WhenReading\":\"read\"", Serialize(new WriteModel(), options));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void WhenReadingDefault_also_skips_the_type_default(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        ReadingDefaultModel model = Deserialize<ReadingDefaultModel>("""{"Name":null,"Age":0}""", options);

        Assert.Equal("initial", model.Name);
        Assert.Equal(41, model.Age);

        ReadingDefaultModel assigned = Deserialize<ReadingDefaultModel>("""{"Name":"x","Age":9}""", options);

        Assert.Equal("x", assigned.Name);
        Assert.Equal(9, assigned.Age);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Read_and_write_conditions_combine_on_one_member(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        CombinedModel model = Deserialize<CombinedModel>("""{"Name":null}""", options);
        Assert.Equal("initial", model.Name);

        Assert.Equal("{}", Serialize(new CombinedModel { Name = null }, options));
        Assert.Equal("""{"Name":"x"}""", Serialize(new CombinedModel { Name = "x" }, options));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void An_unannotated_type_behaves_exactly_as_it_would_without_the_library(SerializerConfiguration configuration)
    {
        JsonSerializerOptions modified = Serializers.Create(configuration);
        JsonSerializerOptions unmodified = Serializers.CreateUnmodified(configuration);

        var value = new UnannotatedModel { Name = "x", Age = 3 };

        Assert.Equal(Serialize(value, unmodified), Serialize(value, modified));

        UnannotatedModel fromModified = Deserialize<UnannotatedModel>("""{"Name":null}""", modified);
        UnannotatedModel fromUnmodified = Deserialize<UnannotatedModel>("""{"Name":null}""", unmodified);

        Assert.Equal(fromUnmodified.Name, fromModified.Name);
        Assert.Null(fromModified.Name);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void RespectNullableAnnotations_does_not_turn_a_skipped_null_into_a_failure(
        SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            configureOptions: static o => o.RespectNullableAnnotations = true);

        ValueModel model = Deserialize<ValueModel>("""{"Age":null}""", options);

        Assert.Equal(41, model.Age);
    }

    private static T Deserialize<T>(string json, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<T>(json, options)!;

    private static string Serialize<T>(T value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(value, options);
}
