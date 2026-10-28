using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Analyzers.Tests;

public sealed class ValueTypeRegistrationGeneratorTests
{
    private const string Imports = """
        using Scarlet.System.Text.Json.IgnoreCondition;

        """;

    private static readonly MetadataReference[] References =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path)),
        MetadataReference.CreateFromFile(typeof(JsonIgnoreExAttribute).Assembly.Location),
    ];

    [Fact]
    public void Custom_value_types_of_annotated_members_are_registered()
    {
        string? generated = Generate(Imports + """
            namespace App;

            public enum Level { Low, High }
            public enum Role { Guest, Admin }
            public struct Point { public int X { get; set; } }
            public readonly record struct Wrapper<T>(T Value);

            public sealed class Settings
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Level Level { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Point? Origin { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)] public Wrapper<int> Wrapped;
            }

            public sealed record Credentials([property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] Role Access = Role.Admin);
            """);

        Assert.NotNull(generated);
        Assert.Contains("RegisterValueType<global::App.Level>();", generated);
        Assert.Contains("RegisterValueType<global::App.Point>();", generated);
        Assert.Contains("RegisterValueType<global::App.Wrapper<int>>();", generated);
        Assert.Contains("RegisterValueType<global::App.Role>();", generated);
        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", generated);
    }

    [Fact]
    public void Each_type_is_registered_once_in_a_stable_order()
    {
        string? generated = Generate(Imports + """
            public enum Beta { A }
            public enum Alpha { A }

            public sealed class First { [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Beta B { get; set; } [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Alpha A { get; set; } }
            public sealed class Second { [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Beta? B { get; set; } }
            """);

        Assert.NotNull(generated);
        Assert.Equal(1, Count(generated, "RegisterValueType<global::Beta>"));
        Assert.True(
            generated.IndexOf("global::Alpha", StringComparison.Ordinal) <
            generated.IndexOf("global::Beta", StringComparison.Ordinal));
    }

    [Fact]
    public void Types_that_need_no_registration_or_cannot_be_named_produce_no_output()
    {
        string? generated = Generate(Imports + """
            using System;
            using System.Collections.Generic;

            public sealed class Model<T> where T : struct
            {
                private enum Hidden { A }

                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public string? Name { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public int Count { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public int? Optional { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public decimal Amount { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public DateTime When { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Guid Id { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public TimeSpan Elapsed { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public List<int> Values { get; set; } = [];
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public T Generic { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public T? NullableGeneric { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] private Hidden Secret { get; set; }
            }
            """);

        Assert.Null(generated);
    }

    [Fact]
    public void Nothing_is_generated_without_annotated_members()
    {
        Assert.Null(Generate("public enum Level { Low, High } public sealed class Model { public Level Level { get; set; } }"));
    }

    [Fact]
    public void Nothing_is_generated_below_CSharp_9()
    {
        Assert.Null(Generate(
            Imports + "public enum Level { Low } public sealed class Model { [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Level Level { get; set; } }",
            LanguageVersion.CSharp8));
    }

    private static string? Generate(string source, LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        var parseOptions = new CSharpParseOptions(languageVersion);
        var compilation = CSharpCompilation.Create(
            "Models",
            [CSharpSyntaxTree.ParseText(source, parseOptions, cancellationToken: TestContext.Current.CancellationToken)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver
            .Create([new ValueTypeRegistrationGenerator().AsSourceGenerator()], parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation, out Compilation output, out _, TestContext.Current.CancellationToken);

        // The generated code has to compile alongside the models it registers.
        Assert.Empty(output.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        return driver.GetRunResult().GeneratedTrees.SingleOrDefault()?.ToString();
    }

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
