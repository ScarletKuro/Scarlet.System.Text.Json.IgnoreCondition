using System.Runtime.CompilerServices;
using VerifyTests;

namespace Scarlet.System.Text.Json.IgnoreCondition.Analyzers.Tests;

public static class ModuleInitializer
{
    // Teaches Verify to snapshot a GeneratorDriver: one verified file per generated source.
    [ModuleInitializer]
    public static void Initialize() => VerifySourceGenerators.Initialize();
}
