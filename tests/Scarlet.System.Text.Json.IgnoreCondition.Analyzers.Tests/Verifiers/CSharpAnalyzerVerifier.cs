using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Scarlet.System.Text.Json.IgnoreCondition.Analyzers.Tests.Verifiers;

// Same Roslyn testing wrapper pattern as MudBlazor.UnitTests.Analyzers.
internal static class CSharpAnalyzerVerifier<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
{
    internal sealed class Test : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    {
        public Test()
        {
            ReferenceAssemblies = ReferenceConfiguration.Assemblies;
            TestState.AdditionalReferences.Add(ReferenceConfiguration.Library);
        }
    }

    internal static Task VerifyAsync(string source)
    {
        var test = new Test { TestCode = source };
        return test.RunAsync(TestContext.Current.CancellationToken);
    }
}
