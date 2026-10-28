using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Analyzers.Tests.Verifiers;

internal static class CSharpCodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new() where TCodeFix : CodeFixProvider, new()
{
    internal sealed class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>
    {
        public Test()
        {
            ReferenceAssemblies = ReferenceConfiguration.Assemblies;
            TestState.AdditionalReferences.Add(ReferenceConfiguration.Library);
        }
    }

    internal static Task VerifyAsync(string source, string fixedSource, string equivalenceKey)
    {
        var test = new Test
        {
            TestCode = source.ReplaceLineEndings(Environment.NewLine),
            FixedCode = fixedSource.ReplaceLineEndings(Environment.NewLine),
            CodeActionEquivalenceKey = equivalenceKey,
        };
        return test.RunAsync(TestContext.Current.CancellationToken);
    }
}
