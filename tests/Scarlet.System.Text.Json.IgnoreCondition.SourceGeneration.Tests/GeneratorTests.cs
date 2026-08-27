using Xunit;

namespace Scarlet.Text.Json.SourceGeneration.Tests;

public sealed class GeneratorTests
{
    private const string Preamble = """
        using System;
        using System.Collections.Generic;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using Scarlet.Text.Json.Serialization;
        using Scarlet.Text.Json.Serialization.Metadata;

        """;

    [Fact]
    public void A_reference_member_is_registered_through_Member()
    {
        GeneratorRun run = Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public string? Name { get; set; } = "initial";
            }
            """);

        Assert.Empty(run.CompilationErrors);
        Assert.Contains(
            """builder.Member<string>("Name", global::Scarlet.Text.Json.Serialization.JsonIgnoreConditionEx.WhenReadingNull);""",
            run.GeneratedSource);
        Assert.Contains("Register(typeof(global::Sample.Model)", run.GeneratedSource);
    }

    [Fact]
    public void A_non_nullable_value_member_is_registered_through_ValueMember()
    {
        GeneratorRun run = Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public int Age { get; set; } = 41;

                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public int? Optional { get; set; } = 7;
            }
            """);

        Assert.Contains("""builder.ValueMember<int>("Age",""", run.GeneratedSource);
        Assert.Contains("""builder.Member<global::System.Nullable<int>>("Optional",""", run.GeneratedSource);
    }

    [Fact]
    public void A_nested_type_is_fully_qualified()
    {
        GeneratorRun run = Run("""
            namespace Sample;

            public static class Outer
            {
                public sealed class Inner
                {
                    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                    public string? Name { get; set; }
                }
            }
            """);

        Assert.Empty(run.CompilationErrors);
        Assert.Contains("Register(typeof(global::Sample.Outer.Inner)", run.GeneratedSource);
    }

    [Fact]
    public void A_record_parameter_with_a_property_target_is_registered()
    {
        GeneratorRun run = Run("""
            namespace Sample;

            public sealed record Model(
                [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string Name = "unknown");
            """);

        Assert.Contains("""builder.Member<string>("Name",""", run.GeneratedSource);
    }

    [Fact]
    public void A_record_parameter_without_a_target_is_registered()
    {
        GeneratorRun run = Run("""
            namespace Sample;

            public sealed record Model(
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string Name = "unknown");
            """);

        Assert.Contains("""builder.Member<string>("Name",""", run.GeneratedSource);
    }

    [Fact]
    public void A_member_reached_twice_is_registered_once()
    {
        GeneratorRun run = Run("""
            namespace Sample;

            public sealed record Model(
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string Name = "unknown");
            """);

        int occurrences = run.GeneratedSource.Split("""builder.Member<string>("Name",""").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void A_field_is_registered()
    {
        GeneratorRun run = Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonInclude]
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public string? Name = "initial";
            }
            """);

        Assert.Contains("""builder.Member<string>("Name",""", run.GeneratedSource);
    }

    [Fact]
    public void Both_sides_of_one_member_are_registered()
    {
        GeneratorRun run = Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenWritingNull)]
                public string? Name { get; set; }
            }
            """);

        Assert.Contains("JsonIgnoreConditionEx.WhenReadingNull);", run.GeneratedSource);
        Assert.Contains("JsonIgnoreConditionEx.WhenWritingNull);", run.GeneratedSource);
    }

    [Fact]
    public void The_parameterless_attribute_means_Always()
    {
        GeneratorRun run = Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnoreEx]
                public string? Name { get; set; }
            }
            """);

        Assert.Contains("JsonIgnoreConditionEx.Always);", run.GeneratedSource);
    }

    [Fact]
    public void The_named_Condition_argument_is_read()
    {
        GeneratorRun run = Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnoreEx(Condition = JsonIgnoreConditionEx.WhenReadingNull)]
                public string? Name { get; set; }
            }
            """);

        Assert.Contains("JsonIgnoreConditionEx.WhenReadingNull);", run.GeneratedSource);
    }

    [Fact]
    public void A_module_initializer_is_emitted_by_default()
    {
        GeneratorRun run = Run(SimpleModel);

        Assert.Contains("ModuleInitializer", run.GeneratedSource);
    }

    [Fact]
    public void The_module_initializer_can_be_switched_off()
    {
        GeneratorRun run = GeneratorHarness.Run(Preamble + SimpleModel, autoRegister: false);

        Assert.DoesNotContain("ModuleInitializer", run.GeneratedSource);
        Assert.Contains("internal static void Register()", run.GeneratedSource);
    }

    [Fact]
    public void Nothing_is_emitted_without_the_attribute()
    {
        GeneratorRun run = Run("""
            namespace Sample;

            public sealed class Model
            {
                public string? Name { get; set; }
            }
            """);

        Assert.Equal(string.Empty, run.GeneratedSource);
    }

    private const string SimpleModel = """
        namespace Sample;

        public sealed class Model
        {
            [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
            public string? Name { get; set; }
        }

        public static class Setup
        {
            public static void Configure(JsonSerializerOptions options) =>
                options.AddExtendedIgnoreConditions();
        }
        """;

    private static GeneratorRun Run(string source) => GeneratorHarness.Run(Preamble + source);
}
