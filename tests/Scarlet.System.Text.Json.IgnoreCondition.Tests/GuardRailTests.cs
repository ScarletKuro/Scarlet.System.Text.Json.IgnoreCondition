using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>
/// Cases where a read-side condition cannot be honoured. Each one has to fail loudly, naming the member
/// and the reason, rather than silently doing nothing.
/// </summary>
/// <remarks>
/// These run against the reflection resolver only: the mechanism selection lives in the contract
/// modifier and is resolver-independent, and the models are deliberately absent from the generated
/// contexts.
/// </remarks>
public sealed class GuardRailTests
{
    public sealed class RequiredMemberModel
    {
        [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        public required string Value { get; set; }
    }

    public sealed class ReadOnlyMemberModel
    {
        [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        public string Value => "read-only";
    }

    public sealed class ExtensionDataModel
    {
        [JsonExtensionData]
        [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        public Dictionary<string, JsonElement> Extra { get; set; } = [];
    }


    public sealed record ComplexParameterModel(
        [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] List<string> Values);

    public sealed record ObjectParameterModel(
        [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] object? Payload = null);

    public sealed record NumberHandlingParameterModel(
        [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] int Count = 3);

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public sealed record ClassNumberHandlingParameterModel(
        [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] int Count = 3);

    public sealed record PlainNumberParameterModel(
        [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] int Count = 3);

    public sealed record NullFactoryParameterModel(
        [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        [property: JsonConverter(typeof(NullConverterFactory))] int Count = 3);

    /// <summary>
    /// A converter for a base type that claims every type. The serializer accepts it on an int member and
    /// adapts it internally; the library can only wrap a converter for the exact member type.
    /// </summary>
    public sealed class BaseTypeConverter : JsonConverter<object>
    {
        public override bool CanConvert(Type typeToConvert) => true;

        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String ? int.Parse(reader.GetString()!, CultureInfo.InvariantCulture) : reader.GetInt32();

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
            writer.WriteNumberValue((int)value);
    }

    public sealed class BaseTypeConverterPropertyModel
    {
        [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        [JsonConverter(typeof(BaseTypeConverter))]
        public int Count { get; set; } = 3;
    }

    public sealed record BaseTypeConverterParameterModel(
        [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
        [property: JsonConverter(typeof(BaseTypeConverter))] int Count = 3);

    /// <summary>A factory that claims the type but creates nothing.</summary>
    public sealed class NullConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => true;

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) => null;
    }

    /// <summary>
    /// Every refused model that is otherwise valid, for the Skip theory. NullFactoryParameterModel is not:
    /// with the condition skipped, the serializer itself rejects a factory that creates no converter.
    /// </summary>
    public static TheoryData<Type> RefusedModels =>
    [
        typeof(RequiredMemberModel),
        typeof(ReadOnlyMemberModel),
        typeof(ExtensionDataModel),
        typeof(ComplexParameterModel),
        typeof(ObjectParameterModel),
        typeof(NumberHandlingParameterModel),
        typeof(ClassNumberHandlingParameterModel),
        typeof(BaseTypeConverterPropertyModel),
        typeof(BaseTypeConverterParameterModel),
    ];


    [Fact]
    public void A_required_member_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<RequiredMemberModel>("""{"Value":null}"""));

        Assert.Contains("required", error.Message);
        Assert.Contains("RequiredMemberModel.Value", error.Message);
    }

    [Fact]
    public void A_member_with_no_setter_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<ReadOnlyMemberModel>("""{"Value":null}"""));

        Assert.Contains("no setter", error.Message);
        Assert.Contains("ReadOnlyMemberModel.Value", error.Message);
    }

    [Fact]
    public void The_extension_data_member_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<ExtensionDataModel>("""{"anything":1}"""));

        Assert.Contains("extension data", error.Message);
    }

    [Fact]
    public void A_constructor_parameter_of_a_non_converter_type_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<ComplexParameterModel>("""{"Values":null}"""));

        Assert.Contains("single-token converter", error.Message);
    }

    /// <summary>
    /// <c>object</c> is serialized polymorphically: the serializer dispatches on the runtime type and resolves
    /// <c>$ref</c>s. A wrapping converter calls the object converter directly, which bypasses both and would
    /// write a boxed <c>2</c> as <c>{}</c>, so the condition is refused rather than losing data.
    /// </summary>
    [Fact]
    public void A_constructor_parameter_of_type_object_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<ObjectParameterModel>("""{"Payload":null}"""));

        Assert.Contains("single-token converter", error.Message);
    }

    [Fact]
    public void A_numeric_constructor_parameter_with_number_handling_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<NumberHandlingParameterModel>("""{"Count":null}"""));

        Assert.Contains("JsonNumberHandling", error.Message);
    }

    /// <summary>
    /// Number handling also reaches a parameter from its declaring type or from the options, and dropping
    /// it there would be just as silent.
    /// </summary>
    [Fact]
    public void Number_handling_from_the_type_or_the_options_is_refused_too()
    {
        NotSupportedException fromType = Assert.Throws<NotSupportedException>(() =>
            Deserialize<ClassNumberHandlingParameterModel>("""{"Count":"5"}"""));
        Assert.Contains("JsonNumberHandling", fromType.Message);

        JsonSerializerOptions options = Serializers.Create(
            SerializerConfiguration.Reflection,
            configureOptions: static options => options.NumberHandling = JsonNumberHandling.AllowReadingFromString);
        NotSupportedException fromOptions = Assert.Throws<NotSupportedException>(() =>
            Serializers.Deserialize<PlainNumberParameterModel>("""{"Count":"5"}""", options));
        Assert.Contains("JsonNumberHandling", fromOptions.Message);
    }

    /// <summary>
    /// Settable value types are re-declared as T? around the member's converter, and constructor
    /// parameters get a substituting converter around it; both need it to convert exactly T. Before this
    /// was checked, the constructor path failed with an InvalidCastException.
    /// </summary>
    [Fact]
    public void A_member_converter_for_a_base_type_is_refused_on_settable_members_and_parameters()
    {
        NotSupportedException property = Assert.Throws<NotSupportedException>(() =>
            Deserialize<BaseTypeConverterPropertyModel>("""{"Count":"7"}"""));
        Assert.Contains("only a converter for the member's own type can be wrapped", property.Message);
        Assert.Contains(nameof(BaseTypeConverter), property.Message);

        NotSupportedException parameter = Assert.Throws<NotSupportedException>(() =>
            Deserialize<BaseTypeConverterParameterModel>("""{"Count":"7"}"""));
        Assert.Contains("only a converter for the member's own type can be wrapped", parameter.Message);
    }

    [Fact]
    public void Skip_leaves_a_base_type_member_converter_to_the_serializer()
    {
        JsonSerializerOptions options = Serializers.Create(
            SerializerConfiguration.Reflection,
            static settings => settings.UnsupportedMemberBehavior = UnsupportedMemberBehavior.Skip);

        Assert.Equal(7, Serializers.Deserialize<BaseTypeConverterPropertyModel>("""{"Count":"7"}""", options).Count);
        Assert.Equal(7, Serializers.Deserialize<BaseTypeConverterParameterModel>("""{"Count":"7"}""", options).Count);
    }

    [Fact]
    public void A_converter_factory_that_creates_no_converter_is_refused()
    {
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Deserialize<NullFactoryParameterModel>("""{"Count":null}"""));

        Assert.Contains("did not create a converter", error.Message);
        Assert.Contains(nameof(NullConverterFactory), error.Message);
    }

    /// <summary>
    /// Skip must leave every refused member exactly as the serializer would have it: no exception, and
    /// no half-applied condition.
    /// </summary>
    [Theory]
    [MemberData(nameof(RefusedModels))]
    public void Skip_resolves_every_refused_member_without_throwing(Type modelType)
    {
        JsonSerializerOptions options = Serializers.Create(
            SerializerConfiguration.Reflection,
            static settings => settings.UnsupportedMemberBehavior = UnsupportedMemberBehavior.Skip);
        options.MakeReadOnly();

        Assert.NotNull(options.GetTypeInfo(modelType));
    }

    [Fact]
    public void Skip_leaves_the_built_in_behaviour_in_place_instead_of_throwing()
    {
        JsonSerializerOptions options = Serializers.Create(
            SerializerConfiguration.Reflection,
            static settings => settings.UnsupportedMemberBehavior = UnsupportedMemberBehavior.Skip);

        RequiredMemberModel model = Serializers.Deserialize<RequiredMemberModel>(
            """{"Value":null}""",
            options);

        Assert.Null(model.Value);
    }

    private static T Deserialize<T>(string json) =>
        Serializers.Deserialize<T>(json, Serializers.Create(SerializerConfiguration.Reflection));
}
