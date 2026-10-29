using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Scarlet.System.Text.Json.IgnoreCondition.Analyzers;

/// <summary>
/// Emits a module initializer that calls <c>ExtendedIgnoreConditionResolver.RegisterValueType&lt;T&gt;()</c>
/// for every custom enum or struct used by a member that carries <c>JsonIgnoreEx</c>, so NativeAOT builds
/// need no manual registration for them.
/// </summary>
/// <remarks>
/// Members reached only by an options-level default, and members whose type is a type parameter, are not
/// visible here; the runtime reports those, and <c>RequireValueTypeRegistration</c> surfaces them under JIT.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class ValueTypeRegistrationGenerator : IIncrementalGenerator
{
    private const string AttributeName = "Scarlet.System.Text.Json.IgnoreCondition.JsonIgnoreExAttribute";
    private const string ResolverName = "Scarlet.System.Text.Json.IgnoreCondition.ExtendedIgnoreConditionResolver";
    private const string ModuleInitializerName = "System.Runtime.CompilerServices.ModuleInitializerAttribute";

    // Value types the runtime library registers itself (MemberOps' static constructor), beyond those
    // with a SpecialType. Registering them again would be harmless, only noisy.
    private static readonly ImmutableHashSet<string> RootedTypes = ImmutableHashSet.Create(
        "System.Guid", "System.DateTimeOffset", "System.DateOnly", "System.TimeOnly", "System.TimeSpan",
        "System.Half", "System.Int128", "System.UInt128");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types = context.SyntaxProvider.ForAttributeWithMetadataName(
                AttributeName,
                static (_, _) => true,
                static (attributed, _) => GetRegistrableType(attributed.TargetSymbol, attributed.SemanticModel.Compilation))
            .Where(static name => name is not null)
            .Collect();

        // [property: JsonIgnoreEx] on a positional record parameter lands on the synthesized property,
        // which ForAttributeWithMetadataName does not report, so those parameters are found by syntax.
        var recordTypes = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ParameterSyntax { Parent.Parent: RecordDeclarationSyntax } parameter &&
                                    parameter.AttributeLists.Any(static list =>
                                        list.Target?.Identifier.IsKind(SyntaxKind.PropertyKeyword) == true),
                static (syntax, token) => GetRecordParameterType(syntax, token))
            .Where(static name => name is not null)
            .Collect();

        var canEmit = context.CompilationProvider.Select(static (compilation, _) => CanEmit(compilation));

        context.RegisterSourceOutput(types.Combine(recordTypes).Combine(canEmit), static (output, source) =>
        {
            var all = source.Left.Left.AddRange(source.Left.Right);
            if (!source.Right || all.IsEmpty)
                return;

            output.AddSource("JsonIgnoreExValueTypeRegistrations.g.cs", Render(all));
        });
    }

    private static string? GetRecordParameterType(GeneratorSyntaxContext syntax, CancellationToken token)
    {
        if (syntax.SemanticModel.GetDeclaredSymbol(syntax.Node, token) is not IParameterSymbol { ContainingType: { } record } parameter)
            return null;

        var property = record.GetMembers(parameter.Name).OfType<IPropertySymbol>().FirstOrDefault();
        if (property is null || !property.GetAttributes().Any(static attribute =>
                attribute.AttributeClass?.ToDisplayString() == AttributeName))
            return null;

        return GetRegistrableType(property, syntax.SemanticModel.Compilation);
    }

    private static string? GetRegistrableType(ISymbol member, Compilation compilation)
    {
        var type = member switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => null,
        };

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];

        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Struct or TypeKind.Enum } named ||
            named.SpecialType != SpecialType.None ||
            RootedTypes.Contains(named.ToDisplayString()) ||
            !CanBeNamedFromGeneratedCode(named) ||
            !compilation.IsSymbolAccessibleWithin(named, compilation.Assembly))
            return null;

        return named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Whether the generated file can spell the type: it must be closed, and neither it, a type containing
    /// it, nor any of its type arguments may be file-local. Assembly accessibility is not enough, because a
    /// file-local type is visible only inside the file that declares it (CS0400 elsewhere).
    /// </summary>
    private static bool CanBeNamedFromGeneratedCode(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal)
                return false;

            foreach (var argument in current.TypeArguments)
            {
                if (argument is ITypeParameterSymbol or IArrayTypeSymbol or IPointerTypeSymbol or IFunctionPointerTypeSymbol ||
                    argument is INamedTypeSymbol namedArgument && !CanBeNamedFromGeneratedCode(namedArgument))
                    return false;
            }
        }

        return true;
    }

    private static bool CanEmit(Compilation compilation) =>
        compilation is CSharpCompilation { LanguageVersion: >= LanguageVersion.CSharp9 } &&
        compilation.GetTypeByMetadataName(ResolverName) is not null &&
        compilation.GetTypeByMetadataName(ModuleInitializerName) is not null;

    private static string Render(ImmutableArray<string?> types)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("// Registers the custom value types used by [JsonIgnoreEx] members, for NativeAOT.");
        builder.AppendLine();
        builder.AppendLine("namespace Scarlet.System.Text.Json.IgnoreCondition.Generated");
        builder.AppendLine("{");
        builder.AppendLine("    [global::System.CodeDom.Compiler.GeneratedCode(\"Scarlet.System.Text.Json.IgnoreCondition\", \"1.0.0\")]");
        builder.AppendLine("    internal static class JsonIgnoreExValueTypeRegistrations");
        builder.AppendLine("    {");
        builder.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        builder.AppendLine("        internal static void Register()");
        builder.AppendLine("        {");

        foreach (var type in new SortedSet<string>(types.Select(static type => type!), global::System.StringComparer.Ordinal))
            builder.AppendLine($"            global::{ResolverName}.RegisterValueType<{type}>();");

        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }
}
