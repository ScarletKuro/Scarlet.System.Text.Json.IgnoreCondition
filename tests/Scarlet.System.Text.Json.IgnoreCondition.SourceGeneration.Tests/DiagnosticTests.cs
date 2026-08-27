using Xunit;

namespace Scarlet.Text.Json.SourceGeneration.Tests;

/// <summary>One test per SJIC rule, so misuse is caught at compile time rather than at run time.</summary>
public sealed class DiagnosticTests
{
    private const string Preamble = """
        using System;
        using System.Collections.Generic;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using Scarlet.Text.Json.Serialization;
        using Scarlet.Text.Json.Serialization.Metadata;

        """;

    private const string Install = """

        public static class Setup
        {
            public static void Configure(JsonSerializerOptions options) =>
                options.AddExtendedIgnoreConditions();
        }
        """;

    [Fact]
    public void SJIC001_required_member()
    {
        Assert.Contains("SJIC001", Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public required string Value { get; set; }
            }
            """));
    }

    [Fact]
    public void SJIC002_no_setter()
    {
        Assert.Contains("SJIC002", Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public string Value { get; } = "read-only";
            }
            """));
    }

    [Fact]
    public void SJIC003_combined_with_JsonIgnore()
    {
        Assert.Contains("SJIC003", Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnore]
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public string? Value { get; set; }
            }
            """));
    }

    [Fact]
    public void SJIC004_two_conditions_on_the_same_side()
    {
        Assert.Contains("SJIC004", Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)]
                public string? Value { get; set; }
            }
            """));
    }

    [Fact]
    public void SJIC005_constructor_parameter_of_a_non_converter_type()
    {
        Assert.Contains("SJIC005", Run("""
            namespace Sample;

            public sealed record Model(
                [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] List<string> Values);
            """));
    }

    [Fact]
    public void SJIC006_number_handling_on_a_constructor_parameter()
    {
        Assert.Contains("SJIC006", Run("""
            namespace Sample;

            public sealed record Model(
                [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] int Count = 3);
            """));
    }

    [Fact]
    public void SJIC007_extension_data()
    {
        Assert.Contains("SJIC007", Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonExtensionData]
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public Dictionary<string, JsonElement>? Extra { get; set; }
            }
            """));
    }

    [Fact]
    public void SJIC008_null_silently_skipped_on_a_value_type()
    {
        Assert.Contains("SJIC008", Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public int Age { get; set; } = 41;
            }
            """));
    }

    [Fact]
    public void SJIC009_resolver_never_installed()
    {
        GeneratorRun run = GeneratorHarness.Run(Preamble + """
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public string? Value { get; set; }
            }
            """);

        Assert.Contains("SJIC009", run.DiagnosticIds);
    }

    [Fact]
    public void SJIC009_is_silent_once_the_resolver_is_installed()
    {
        Assert.DoesNotContain("SJIC009", Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public string? Value { get; set; }
            }
            """));
    }

    [Fact]
    public void SJIC010_generic_type_is_not_registered()
    {
        GeneratorRun run = RunFull("""
            namespace Sample;

            public sealed class Model<T>
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public string? Value { get; set; }
            }
            """);

        Assert.Contains("SJIC010", run.DiagnosticIds);
        Assert.Equal(string.Empty, run.GeneratedSource);
    }

    [Fact]
    public void SJIC011_inaccessible_type_is_not_registered()
    {
        GeneratorRun run = RunFull("""
            namespace Sample;

            public sealed class Outer
            {
                private sealed class Inner
                {
                    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                    public string? Value { get; set; }
                }
            }
            """);

        Assert.Contains("SJIC011", run.DiagnosticIds);
        Assert.Equal(string.Empty, run.GeneratedSource);
    }

    [Fact]
    public void A_correct_model_produces_no_diagnostics_beyond_the_informational_ones()
    {
        IEnumerable<string> ids = Run("""
            namespace Sample;

            public sealed class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenWritingNull)]
                public string? Value { get; set; } = "initial";
            }
            """);

        Assert.Empty(ids);
    }

    private static IEnumerable<string> Run(string source) => RunFull(source).DiagnosticIds;

    private static GeneratorRun RunFull(string source) => GeneratorHarness.Run(Preamble + source + Install);
}
