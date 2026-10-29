using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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

    /// <summary>
    /// Snapshot of the full generated source, in ValueTypeRegistrations#…verified.cs next to this file. Covers an enum, a
    /// nullable struct, a closed generic struct and a record parameter's property: target.
    /// </summary>
    [Fact]
    public Task Custom_value_types_of_annotated_members_are_registered()
    {
        GeneratorDriver driver = Run(Imports + """
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

        // Named explicitly: the default <class>.<method>#<hint name> file name pushes the full path past
        // Windows' 260-character limit, which git for Windows refuses to stage.
        return Verify(driver).UseFileName("ValueTypeRegistrations");
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

    /// <summary>
    /// A file-local type is accessible within the assembly but not from the generated file, so naming it
    /// there would be CS0400. That holds for the type itself, for a type nested inside a file-local type,
    /// and for a file-local type used as a generic argument. Generate() fails on any compile error.
    /// </summary>
    [Fact]
    public void File_local_types_are_not_registered()
    {
        string? generated = Generate(Imports + """
            public readonly record struct Wrapper<T>(T Value);

            file enum Hidden { A }

            file sealed class Outer
            {
                public enum Nested { A }
            }

            file sealed class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Hidden Direct { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Hidden? Optional { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Outer.Nested Nested { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Wrapper<Hidden> Wrapped { get; set; }
            }
            """);

        Assert.Null(generated);
    }

    [Fact]
    public void Closed_array_type_arguments_can_be_registered()
    {
        string? generated = Generate(Imports + """
            public readonly record struct Wrapper<T>(T Value);
            public sealed class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Wrapper<int[]> Vector { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)] public Wrapper<string[][]> Jagged { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Wrapper<int[,]> Matrix { get; set; }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Wrapper<dynamic[]> Dynamic { get; set; }
            }
            """);

        Assert.NotNull(generated);
        Assert.Contains("RegisterValueType<global::Wrapper<int[]>>", generated);
        Assert.Contains("RegisterValueType<global::Wrapper<string[][]>>", generated);
        Assert.Contains("RegisterValueType<global::Wrapper<int[,]>>", generated);
        Assert.Contains("RegisterValueType<global::Wrapper<dynamic[]>>", generated);
    }

    [Fact]
    public void Open_and_file_local_array_arguments_cannot_be_registered()
    {
        string? generated = Generate(Imports + """
            public readonly record struct Wrapper<T>(T Value);
            public sealed class GenericModel<T>
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Wrapper<T[]> Value { get; set; }
            }
            file enum Hidden { A }
            file sealed class LocalModel
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public Wrapper<Hidden[]> Value { get; set; }
            }
            """);

        Assert.Null(generated);
    }

    [Fact]
    public void Private_generic_arguments_cannot_be_registered()
    {
        string? generated = Generate(Imports + """
            public readonly record struct Wrapper<T>(T Value);
            public sealed class Model
            {
                private enum Hidden { A }
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] private Wrapper<Hidden> Value { get; set; }
            }
            """);

        Assert.Null(generated);
    }

    [Fact]
    public void Ref_structs_cannot_be_registered_as_generic_type_arguments()
    {
        string? generated = Generate(Imports + """
            using System.Text.Json.Serialization;
            public ref struct RefValue { }
            public sealed class Model
            {
                [JsonIgnore]
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] public RefValue Value => default;
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

    private static string? Generate(string source, LanguageVersion languageVersion = LanguageVersion.Latest) =>
        Run(source, languageVersion).GetRunResult().GeneratedTrees.SingleOrDefault()?.ToString();

    private static GeneratorDriver Run(string source, LanguageVersion languageVersion = LanguageVersion.Latest)
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

        return driver;
    }

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
