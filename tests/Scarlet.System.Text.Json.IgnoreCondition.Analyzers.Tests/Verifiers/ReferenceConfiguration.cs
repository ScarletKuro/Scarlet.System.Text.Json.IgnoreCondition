using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Scarlet.System.Text.Json.IgnoreCondition.Analyzers.Tests.Verifiers;

internal static class ReferenceConfiguration
{
    internal static readonly ReferenceAssemblies Assemblies = new(
        "net10.0", new PackageIdentity("Microsoft.NETCore.App.Ref", "10.0.0"), Path.Combine("ref", "net10.0"));

    internal static readonly MetadataReference Library = MetadataReference.CreateFromFile(typeof(JsonIgnoreExAttribute).Assembly.Location);
}
