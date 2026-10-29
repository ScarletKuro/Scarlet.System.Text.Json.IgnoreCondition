using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Scarlet.System.Text.Json.IgnoreCondition.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class IgnoreConditionAnalyzer : DiagnosticAnalyzer
{
    private const string ExtendedAttributeName = "Scarlet.System.Text.Json.IgnoreCondition.JsonIgnoreExAttribute";
    private const string NativeAttributeName = "System.Text.Json.Serialization.JsonIgnoreAttribute";

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var extendedType = start.Compilation.GetTypeByMetadataName(ExtendedAttributeName);
            var nativeType = start.Compilation.GetTypeByMetadataName(NativeAttributeName);
            if (extendedType is null || nativeType is null)
            {
                return;
            }

            start.RegisterSymbolAction(c => Analyze(c, extendedType, nativeType), SymbolKind.Property, SymbolKind.Field);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol extendedType, INamedTypeSymbol nativeType)
    {
        // A field declaration shares its attributes across all of its variable symbols.
        if (context.Symbol is IFieldSymbol field &&
            field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken) is VariableDeclaratorSyntax variable &&
            variable.Parent is VariableDeclarationSyntax declaration && declaration.Variables[0] != variable)
            return;

        var extended = new List<(AttributeData Attribute, string Condition)>();
        AttributeData? native = null;
        int nativeCondition = 1;
        foreach (var attribute in context.Symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, extendedType))
            {
                // No list pattern here: netstandard2.0 has no System.Index, which list patterns require.
                if (attribute.ConstructorArguments.Length == 1 &&
                    attribute.ConstructorArguments[0].Value is int value and (0 or 1))
                {
                    extended.Add((attribute, value == 0 ? "WhenReadingNull" : "WhenReadingDefault"));
                }
            }
            else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, nativeType))
            {
                native = attribute;
                foreach (var argument in attribute.NamedArguments)
                {
                    if (argument is { Key: "Condition", Value.Value: int value })
                    {
                        nativeCondition = value;
                    }
                }
            }
        }

        if (native is null || extended.Count == 0)
        {
            return;
        }

        bool conflicts = IgnoreConditionRules.NativeConflictsWithExtended(nativeCondition);
        if (!conflicts && !IgnoreConditionRules.NativeMasksExtended(nativeCondition))
        {
            return;
        }

        DiagnosticDescriptor descriptor = conflicts ? Conflict : Ineffective;

        var nativeSyntax = native.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken);
        if (nativeSyntax is null)
        {
            return;
        }

        foreach (var item in extended)
        {
            var syntax = item.Attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken);
            if (syntax is null)
            {
                continue;
            }

            object[] arguments = conflicts ? [context.Symbol.Name, item.Condition] : [context.Symbol.Name];
            context.ReportDiagnostic(Diagnostic.Create(descriptor, syntax.GetLocation(),
                additionalLocations: [nativeSyntax.GetLocation()],
                properties: ImmutableDictionary<string, string?>.Empty
                    .Add("Condition", item.Condition).Add("PreviousCondition", "WhenReading"),
                messageArgs: arguments));
        }
    }
}
