using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>
/// The options-level default, which is the replacement for the read-side half of the obsolete
/// <c>JsonSerializerOptions.IgnoreNullValues</c> (dotnet/runtime#62086).
/// </summary>
public sealed class OptionsDefaultTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public void DefaultReadCondition_applies_to_members_with_no_attribute(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull);

        OptionsDefaultModel model = Serializers.Deserialize<OptionsDefaultModel>(
            """{"Name":null,"Values":null,"Optional":null}""",
            options)!;

        Assert.Equal("initial", model.Name);
        Assert.Equal(["a"], model.Values);
        Assert.Equal(7, model.Optional);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Never_opts_a_single_member_out_of_the_default(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull);

        OptionsDefaultModel model = Serializers.Deserialize<OptionsDefaultModel>(
            """{"Name":null,"OptedOut":null}""",
            options)!;

        Assert.Equal("initial", model.Name);
        Assert.Null(model.OptedOut);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Native_write_defaults_apply_to_members_with_no_attribute(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            configureOptions: static options => options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

        string json = Serializers.Serialize(
            new OptionsDefaultModel { Name = null, Optional = null, OptedOut = null },
            options);

        Assert.DoesNotContain("\"Name\"", json);
        Assert.DoesNotContain("\"Optional\"", json);
        Assert.Contains("\"OptedOut\":null", json);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_default_does_not_break_members_it_cannot_apply_to(SerializerConfiguration configuration)
    {
        // WriteModel has read-only-ish and constructor-free members plus explicit conditions; a
        // sweeping default must not throw for any of them.
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull);

        WriteModel model = Serializers.Deserialize<WriteModel>("""{"Kept":null}""", options)!;

        Assert.Equal("kept", model.Kept);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_default_read_condition_reaches_constructor_parameters(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull);

        RecordModel model = Serializers.Deserialize<RecordModel>("""{"Name":null,"Age":null}""", options)!;

        Assert.Equal("unknown", model.Name);
        Assert.Equal(41, model.Age);
    }

    /// <summary>
    /// A custom value type that was never registered: under JIT the default applies, and under NativeAOT
    /// the missing registration must fail at contract resolution rather than be skipped silently, which
    /// would leave a null payload to throw only in the published app.
    /// </summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_default_on_an_unregistered_value_type_is_not_dropped_silently(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull);

        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            UnregisteredValueTypeModel model = Serializers.Deserialize<UnregisteredValueTypeModel>(
                """{"Shade":null}""",
                options)!;

            Assert.Equal(Shade.Dark, model.Shade);
        }
        else
        {
            NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
                Serializers.Deserialize<UnregisteredValueTypeModel>("{}", options));

            Assert.Contains("RegisterValueType<Shade>()", error.Message);
        }
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Skip_drops_a_default_on_an_unregistered_value_type(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings =>
            {
                settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull;
                settings.UnsupportedMemberBehavior = UnsupportedMemberBehavior.Skip;
            });

        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            Assert.Equal(
                Shade.Dark,
                Serializers.Deserialize<UnregisteredValueTypeModel>("""{"Shade":null}""", options)!.Shade);
        }
        else
        {
            Assert.Throws<JsonException>(() =>
                Serializers.Deserialize<UnregisteredValueTypeModel>("""{"Shade":null}""", options));
        }
    }

    /// <summary>
    /// RequireValueTypeRegistration makes a JIT run refuse exactly what a NativeAOT build would, so an
    /// application can find missing registrations before it is ever published natively.
    /// </summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void RequireValueTypeRegistration_refuses_an_unregistered_value_type_on_any_runtime(
        SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings =>
            {
                settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull;
                settings.RequireValueTypeRegistration = true;
            });

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Serializers.Deserialize<UnregisteredValueTypeModel>("{}", options));

        Assert.Contains("RegisterValueType<Shade>()", error.Message);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void RequireValueTypeRegistration_names_the_underlying_type_of_a_nullable_member(
        SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings =>
            {
                settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingDefault;
                settings.RequireValueTypeRegistration = true;
            });

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            Serializers.Deserialize<UnregisteredNullableValueTypeModel>("{}", options));

        Assert.Contains("RegisterValueType<Shade>()", error.Message);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void RequireValueTypeRegistration_accepts_generated_registrations(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(
            configuration,
            static settings => settings.RequireValueTypeRegistration = true);

        CustomValueTypeModel model = Serializers.Deserialize<CustomValueTypeModel>(
            """{"Location":null,"Priority":null}""",
            options)!;

        Assert.Equal(new Point(3, 4), model.Location);
        Assert.Equal(Priority.High, model.Priority);
    }

    [Fact]
    public void Settings_changed_after_the_resolver_is_created_have_no_effect()
    {
        var settings = new ExtendedIgnoreConditionSettings();
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new ExtendedIgnoreConditionResolver(DefaultModeContext.Default, settings),
        };

        settings.DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull;

        Assert.Throws<JsonException>(() =>
            Serializers.Deserialize<UnannotatedModel>("""{"Age":null}""", options));
    }

    [Fact]
    public void Undefined_read_conditions_are_rejected()
    {
        var settings = new ExtendedIgnoreConditionSettings();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            settings.DefaultReadCondition = (JsonIgnoreConditionEx)999);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new JsonIgnoreExAttribute((JsonIgnoreConditionEx)999));
    }
}
