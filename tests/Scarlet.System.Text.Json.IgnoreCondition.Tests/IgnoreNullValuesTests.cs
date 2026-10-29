using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>
/// The obsolete <c>JsonSerializerOptions.IgnoreNullValues</c> is what many applications use today as a
/// stand-in for WhenReadingNull. The library has to work alongside it during a migration, and its
/// documented replacement has to write identically and read at least as leniently.
/// </summary>
public sealed class IgnoreNullValuesTests
{
    private const string AllNulls =
        """{"Name":null,"Values":null,"Optional":null,"OptedOut":null,"WriteConditioned":null,"Annotated":null}""";

    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Alongside_IgnoreNullValues_native_behaviour_is_kept_and_JsonIgnoreEx_still_applies(
        SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = WithIgnoreNullValues(configuration);

        IgnoreNullValuesModel model = Serializers.Deserialize<IgnoreNullValuesModel>(AllNulls, options);

        Assert.Equal("initial", model.Name);
        Assert.Equal(["a"], model.Values);
        Assert.Equal(7, model.Optional);
        Assert.Null(model.OptedOut);

        // Native quirk: any [JsonIgnore(Condition = ...)], even a write-only one, switches
        // IgnoreNullValues off for that member on the read side as well.
        Assert.Null(model.WriteConditioned);

        // IgnoreNullValues never covers a non-nullable value type; the explicit attribute does.
        Assert.Equal(5, model.Annotated);
        Assert.Throws<JsonException>(() =>
            Serializers.Deserialize<IgnoreNullValuesModel>("""{"Count":null}""", options));
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void The_replacement_skips_every_null_IgnoreNullValues_skips_and_more(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = WithReplacement(configuration);

        IgnoreNullValuesModel model = Serializers.Deserialize<IgnoreNullValuesModel>(
            AllNulls.Replace("{", """{"Count":null,""", StringComparison.Ordinal),
            options);

        Assert.Equal("initial", model.Name);
        Assert.Equal(["a"], model.Values);
        Assert.Equal(7, model.Optional);
        Assert.Equal(3, model.Count);
        Assert.Null(model.OptedOut);
        Assert.Equal("initial", model.WriteConditioned);
        Assert.Equal(5, model.Annotated);

        IgnoreNullValuesRecord record = Serializers.Deserialize<IgnoreNullValuesRecord>(
            """{"Name":null,"Age":null}""",
            options);

        Assert.Equal("unknown", record.Name);
        Assert.Equal(41, record.Age);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void The_replacement_writes_exactly_what_IgnoreNullValues_writes(SerializerConfiguration configuration)
    {
        var model = new IgnoreNullValuesModel
        {
            Name = null,
            Values = null!,
            Optional = null,
            OptedOut = null,
            WriteConditioned = null,
        };

        string legacy = Serializers.Serialize(model, WithIgnoreNullValues(configuration));
        string replacement = Serializers.Serialize(model, WithReplacement(configuration));

        Assert.Equal(legacy, replacement);
        Assert.Equal("""{"Count":3,"OptedOut":null,"Annotated":5}""", replacement);
    }

    [Fact]
    public void IgnoreNullValues_cannot_be_combined_with_DefaultIgnoreCondition() =>
        Assert.Throws<InvalidOperationException>(() => new JsonSerializerOptions
        {
#pragma warning disable SYSLIB0020 // The obsolete option is the subject of the test.
            IgnoreNullValues = true,
#pragma warning restore SYSLIB0020
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });

    private static JsonSerializerOptions WithIgnoreNullValues(SerializerConfiguration configuration) =>
        Serializers.Create(
            configuration,
#pragma warning disable SYSLIB0020 // The obsolete option is the subject of the test.
            configureOptions: static options => options.IgnoreNullValues = true);
#pragma warning restore SYSLIB0020

    private static JsonSerializerOptions WithReplacement(SerializerConfiguration configuration) =>
        Serializers.Create(
            configuration,
            static settings => settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull,
            static options => options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
}
