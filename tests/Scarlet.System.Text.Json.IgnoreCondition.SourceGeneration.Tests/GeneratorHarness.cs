using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Scarlet.Text.Json.Serialization;

namespace Scarlet.Text.Json.SourceGeneration.Tests;

/// <summary>Runs the generator over a source snippet in process.</summary>
internal static class GeneratorHarness
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    public static GeneratorRun Run(string source, bool autoRegister = true)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var options = new TestAnalyzerConfigOptionsProvider(autoRegister);

        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(new JsonIgnoreConditionGenerator().AsSourceGenerator())
            .WithUpdatedAnalyzerConfigOptions(options);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation updated,
            out ImmutableArray<Diagnostic> _);

        GeneratorDriverRunResult result = driver.GetRunResult();

        string generated = result.Results
            .SelectMany(static r => r.GeneratedSources)
            .Select(static s => s.SourceText.ToString())
            .FirstOrDefault() ?? string.Empty;

        ImmutableArray<Diagnostic> compilationDiagnostics = updated
            .GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        return new GeneratorRun(generated, result.Diagnostics, compilationDiagnostics);
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        foreach (string assembly in (string[])
                 ["System.Private.CoreLib.dll", "System.Runtime.dll", "System.Collections.dll", "System.Text.Json.dll"])
        {
            string path = Path.Combine(runtimeDirectory, assembly);
            if (File.Exists(path))
            {
                builder.Add(MetadataReference.CreateFromFile(path));
            }
        }

        builder.Add(MetadataReference.CreateFromFile(typeof(JsonIgnoreExAttribute).Assembly.Location));
        return builder.ToImmutable();
    }

    private sealed class TestAnalyzerConfigOptionsProvider(bool autoRegister) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new TestAnalyzerConfigOptions(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["build_property.ScarletJsonIgnoreConditionAutoRegister"] = autoRegister ? "true" : "false",
            });

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }

    private sealed class TestAnalyzerConfigOptions(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}

internal sealed record GeneratorRun(
    string GeneratedSource,
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    ImmutableArray<Diagnostic> CompilationErrors)
{
    public IEnumerable<string> DiagnosticIds => GeneratorDiagnostics.Select(static d => d.Id);
}
