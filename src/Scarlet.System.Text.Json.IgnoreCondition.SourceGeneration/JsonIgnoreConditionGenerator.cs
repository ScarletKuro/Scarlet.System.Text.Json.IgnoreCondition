using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Scarlet.Text.Json.SourceGeneration;

/// <summary>
/// Emits the compile-time ignore-condition registry for every type in the compilation that uses
/// <c>[JsonIgnoreEx]</c>, and reports the SJIC diagnostics.
/// </summary>
/// <remarks>
/// The registry exists so that the library never has to read attributes by reflection: the emitted
/// code passes each member type as a generic argument, which is what lets the runtime build its
/// converters and nullable projections under NativeAOT.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class JsonIgnoreConditionGenerator : IIncrementalGenerator
{
    internal const string AttributeName = "Scarlet.Text.Json.Serialization.JsonIgnoreExAttribute";
    private const string ConditionEnum = "global::Scarlet.Text.Json.Serialization.JsonIgnoreConditionEx";
    private const string BuilderInterface = "global::Scarlet.Text.Json.Serialization.Metadata.IIgnoreConditionBuilder";
    private const string Registry = "global::Scarlet.Text.Json.Serialization.Metadata.ExtendedIgnoreConditionRegistry";
    private const string GeneratorVersion = "1.0.0";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<MemberAnalysis> attributedMembers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                predicate: static (node, _) => node is PropertyDeclarationSyntax
                    or VariableDeclaratorSyntax
                    or ParameterSyntax
                    or FieldDeclarationSyntax,
                transform: static (syntaxContext, cancellationToken) =>
                    AnalyzeMemberSymbol(syntaxContext.TargetSymbol, syntaxContext.Attributes, cancellationToken))
            .Where(static analysis => analysis is not null)
            .Select(static (analysis, _) => analysis!);

        // ForAttributeWithMetadataName indexes attributes by the declaration they are written on, so
        // a [property:]-targeted attribute on a record's positional parameter never reaches it: the
        // attribute belongs to the generated property, which has no syntax of its own. Those are
        // picked up here instead.
        IncrementalValuesProvider<MemberAnalysis> targetedRecordMembers = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ParameterSyntax { AttributeLists.Count: > 0 } parameter &&
                    HasRedirectedAttributeTarget(parameter),
                transform: static (syntaxContext, cancellationToken) =>
                    AnalyzeRedirectedParameter(syntaxContext, cancellationToken))
            .Where(static analysis => analysis is not null)
            .Select(static (analysis, _) => analysis!);

        IncrementalValuesProvider<MemberAnalysis> members = attributedMembers.Collect()
            .Combine(targetedRecordMembers.Collect())
            .SelectMany(static (both, _) => both.Left.AddRange(both.Right));

        IncrementalValueProvider<bool> resolverInstalled = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is IdentifierNameSyntax identifier &&
                    identifier.Identifier.ValueText is "AddExtendedIgnoreConditions"
                        or "WithExtendedIgnoreConditions"
                        or "CreateModifier",
                transform: static (_, _) => true)
            .Collect()
            .Select(static (matches, _) => matches.Length > 0);

        IncrementalValueProvider<bool> autoRegister = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
                !options.GlobalOptions.TryGetValue("build_property.ScarletJsonIgnoreConditionAutoRegister", out string? value) ||
                !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase));

        var source = members.Collect().Combine(resolverInstalled).Combine(autoRegister);

        context.RegisterSourceOutput(source, static (productionContext, input) =>
        {
            ImmutableArray<MemberAnalysis> analyses = input.Left.Left;
            bool installed = input.Left.Right;
            bool emitModuleInitializer = input.Right;

            Emit(productionContext, analyses, installed, emitModuleInitializer);
        });
    }

    private static void Emit(
        SourceProductionContext context,
        ImmutableArray<MemberAnalysis> analyses,
        bool resolverInstalled,
        bool emitModuleInitializer)
    {
        if (analyses.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (MemberAnalysis analysis in analyses)
        {
            foreach (DiagnosticInfo info in analysis.Diagnostics)
            {
                if (info.ToDiagnostic() is { } diagnostic)
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        if (!resolverInstalled)
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ResolverNotInstalled, Location.None));
        }

        List<TypeRegistration> registrations = Group(analyses);
        if (registrations.Count == 0)
        {
            return;
        }

        context.AddSource(
            "ScarletIgnoreConditions.g.cs",
            Emitter.Emit(registrations, emitModuleInitializer, GeneratorVersion));
    }

    private static List<TypeRegistration> Group(ImmutableArray<MemberAnalysis> analyses)
    {
        var byType = new Dictionary<string, List<MemberAnalysis>>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (MemberAnalysis analysis in analyses)
        {
            if (analysis.Registrations.Count == 0)
            {
                continue;
            }

            if (!byType.TryGetValue(analysis.TypeExpression, out List<MemberAnalysis>? bucket))
            {
                bucket = [];
                byType.Add(analysis.TypeExpression, bucket);
                order.Add(analysis.TypeExpression);
            }

            bucket.Add(analysis);
        }

        var result = new List<TypeRegistration>(order.Count);
        foreach (string typeExpression in order)
        {
            List<MemberAnalysis> bucket = byType[typeExpression];
            // Distinct because a member can be reached both by the attribute index and by the
            // redirected-target scan, and because two attributes may declare the same condition.
            List<MemberRegistration> memberRegistrations = bucket
                .SelectMany(static analysis => analysis.Registrations)
                .Distinct()
                .OrderBy(static registration => registration.MemberName, StringComparer.Ordinal)
                .ThenBy(static registration => registration.BuilderCall, StringComparer.Ordinal)
                .ToList();

            result.Add(new TypeRegistration(typeExpression, bucket[0].SafeName, memberRegistrations, []));
        }

        result.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.TypeExpression, right.TypeExpression));
        return result;
    }

    private static bool HasRedirectedAttributeTarget(ParameterSyntax parameter)
    {
        foreach (AttributeListSyntax list in parameter.AttributeLists)
        {
            if (list.Target?.Identifier.ValueText is "property" or "field")
            {
                return true;
            }
        }

        return false;
    }

    private static MemberAnalysis? AnalyzeRedirectedParameter(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var parameter = (ParameterSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(parameter, cancellationToken) is not { } parameterSymbol ||
            parameterSymbol.ContainingType is not { } containingType)
        {
            return null;
        }

        ISymbol? member = null;
        foreach (ISymbol candidate in containingType.GetMembers(parameterSymbol.Name))
        {
            if (candidate is IPropertySymbol or IFieldSymbol)
            {
                member = candidate;
                break;
            }
        }

        if (member is null)
        {
            return null;
        }

        var attributes = ImmutableArray.CreateBuilder<AttributeData>();
        foreach (AttributeData attribute in member.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == AttributeName)
            {
                attributes.Add(attribute);
            }
        }

        return attributes.Count == 0
            ? null
            : AnalyzeMemberSymbol(member, attributes.ToImmutable(), cancellationToken);
    }

    private static MemberAnalysis? AnalyzeMemberSymbol(
        ISymbol target,
        ImmutableArray<AttributeData> attributeData,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        INamedTypeSymbol? containingType = target.ContainingType;
        if (containingType is null)
        {
            return null;
        }

        var diagnostics = new List<DiagnosticInfo>();
        Location? location = target.Locations.FirstOrDefault();
        string typeDisplay = containingType.ToDisplayString();

        if (!TryGetMemberShape(target, out ITypeSymbol? memberType, out bool isParameterTarget))
        {
            return null;
        }

        List<JsonIgnoreConditionValue> conditions = ReadConditions(attributeData);
        if (conditions.Count == 0)
        {
            return null;
        }

        ValidateSides(conditions, diagnostics, location, typeDisplay, target.Name);

        bool hasReadSide = conditions.Any(static condition => condition.IsReadSide);
        IParameterSymbol? constructorParameter = isParameterTarget
            ? (IParameterSymbol)target
            : FindConstructorParameter(containingType, target.Name, memberType!);
        bool isConstructorBound = constructorParameter is not null;

        AnalyzeMember(
            target,
            memberType!,
            containingType,
            hasReadSide,
            isConstructorBound,
            isParameterTarget,
            conditions,
            diagnostics,
            location,
            typeDisplay);

        bool canRegister = CanRegister(containingType, diagnostics, location, typeDisplay);
        List<MemberRegistration> registrations = canRegister
            ? RenderRegistrations(target.Name, memberType!, conditions)
            : [];

        return new MemberAnalysis(
            containingType.ToDisplayString(FullyQualified),
            SafeName(containingType),
            registrations,
            diagnostics);
    }

    private static bool TryGetMemberShape(ISymbol target, out ITypeSymbol? memberType, out bool isParameterTarget)
    {
        switch (target)
        {
            case IPropertySymbol property:
                memberType = property.Type;
                isParameterTarget = false;
                return true;

            case IFieldSymbol field:
                memberType = field.Type;
                isParameterTarget = false;
                return true;

            case IParameterSymbol parameter:
                memberType = parameter.Type;
                isParameterTarget = true;
                return true;

            default:
                memberType = null;
                isParameterTarget = false;
                return false;
        }
    }

    private static void AnalyzeMember(
        ISymbol target,
        ITypeSymbol memberType,
        INamedTypeSymbol containingType,
        bool hasReadSide,
        bool isConstructorBound,
        bool isParameterTarget,
        List<JsonIgnoreConditionValue> conditions,
        List<DiagnosticInfo> diagnostics,
        Location? location,
        string typeDisplay)
    {
        if (HasAttribute(target, "System.Text.Json.Serialization.JsonIgnoreAttribute"))
        {
            diagnostics.Add(new DiagnosticInfo("SJIC003", location, typeDisplay, target.Name));
        }

        if (HasAttribute(target, "System.Text.Json.Serialization.JsonExtensionDataAttribute"))
        {
            diagnostics.Add(new DiagnosticInfo("SJIC007", location, typeDisplay, target.Name));
        }

        if (!hasReadSide)
        {
            return;
        }

        if (target is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
        {
            diagnostics.Add(new DiagnosticInfo("SJIC001", location, typeDisplay, target.Name));
        }

        if (!isConstructorBound && !CanBeAssigned(target))
        {
            diagnostics.Add(new DiagnosticInfo("SJIC002", location, typeDisplay, target.Name));
        }

        if (isConstructorBound)
        {
            bool hasMemberConverter = HasAttribute(target, "System.Text.Json.Serialization.JsonConverterAttribute");
            if (!TypeClassification.IsConverterBased(memberType, hasMemberConverter))
            {
                diagnostics.Add(new DiagnosticInfo("SJIC005", location, typeDisplay, target.Name));
            }
            else if (TypeClassification.IsNumeric(memberType) &&
                     (HasAttribute(target, "System.Text.Json.Serialization.JsonNumberHandlingAttribute") ||
                      HasAttribute(containingType, "System.Text.Json.Serialization.JsonNumberHandlingAttribute")))
            {
                diagnostics.Add(new DiagnosticInfo("SJIC006", location, typeDisplay, target.Name));
            }
        }

        bool skipsNullSilently = conditions.Any(static condition =>
            condition.Value == JsonIgnoreConditionKind.WhenReadingNull ||
            condition.Value == JsonIgnoreConditionKind.WhenReadingDefault);

        if (skipsNullSilently && TypeClassification.IsNonNullableValueType(memberType) && !isParameterTarget)
        {
            diagnostics.Add(new DiagnosticInfo("SJIC008", location, typeDisplay, target.Name));
        }
    }

    private static bool CanRegister(
        INamedTypeSymbol containingType,
        List<DiagnosticInfo> diagnostics,
        Location? location,
        string typeDisplay)
    {
        for (INamedTypeSymbol? current = containingType; current is not null; current = current.ContainingType)
        {
            if (current.IsGenericType)
            {
                diagnostics.Add(new DiagnosticInfo("SJIC010", location, typeDisplay));
                return false;
            }

            if (current.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected
                or Accessibility.ProtectedAndInternal or Accessibility.ProtectedOrInternal)
            {
                diagnostics.Add(new DiagnosticInfo("SJIC011", location, typeDisplay));
                return false;
            }
        }

        return true;
    }

    private static List<MemberRegistration> RenderRegistrations(
        string memberName,
        ITypeSymbol memberType,
        List<JsonIgnoreConditionValue> conditions)
    {
        string typeArgument = memberType.ToDisplayString(FullyQualified);
        string method = TypeClassification.IsNonNullableValueType(memberType) ? "ValueMember" : "Member";
        var registrations = new List<MemberRegistration>(conditions.Count);

        foreach (JsonIgnoreConditionValue condition in conditions)
        {
            registrations.Add(new MemberRegistration(
                memberName,
                $"builder.{method}<{typeArgument}>(\"{memberName}\", {ConditionEnum}.{condition.Value});"));
        }

        return registrations;
    }

    private static void ValidateSides(
        List<JsonIgnoreConditionValue> conditions,
        List<DiagnosticInfo> diagnostics,
        Location? location,
        string typeDisplay,
        string memberName)
    {
        int reads = conditions.Count(static condition => condition.IsReadSide);
        int writes = conditions.Count(static condition => condition.IsWriteSide);

        if (reads > 1)
        {
            diagnostics.Add(new DiagnosticInfo("SJIC004", location, typeDisplay, memberName, "read"));
        }

        if (writes > 1)
        {
            diagnostics.Add(new DiagnosticInfo("SJIC004", location, typeDisplay, memberName, "write"));
        }
    }

    private static List<JsonIgnoreConditionValue> ReadConditions(ImmutableArray<AttributeData> attributes)
    {
        var conditions = new List<JsonIgnoreConditionValue>(attributes.Length);

        foreach (AttributeData attribute in attributes)
        {
            int? raw = null;

            if (attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Value is int fromConstructor)
            {
                raw = fromConstructor;
            }

            foreach (KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
            {
                if (named.Key == "Condition" && named.Value.Value is int fromProperty)
                {
                    raw = fromProperty;
                }
            }

            // The parameterless constructor defaults to Always.
            JsonIgnoreConditionKind kind = raw is { } value
                ? (JsonIgnoreConditionKind)value
                : JsonIgnoreConditionKind.Always;

            if (Enum.IsDefined(typeof(JsonIgnoreConditionKind), kind))
            {
                conditions.Add(new JsonIgnoreConditionValue(kind));
            }
        }

        return conditions;
    }

    private static bool CanBeAssigned(ISymbol member) => member switch
    {
        IPropertySymbol property => property.SetMethod is not null,
        IFieldSymbol field => !field.IsReadOnly && !field.IsConst,
        _ => false,
    };

    /// <summary>
    /// Mirrors the serializer's constructor selection: an explicitly annotated constructor, else the
    /// only public one.
    /// </summary>
    private static IParameterSymbol? FindConstructorParameter(
        INamedTypeSymbol containingType,
        string memberName,
        ITypeSymbol memberType)
    {
        IMethodSymbol? selected = null;
        IMethodSymbol? singlePublic = null;
        int publicCount = 0;

        foreach (IMethodSymbol constructor in containingType.InstanceConstructors)
        {
            if (constructor.IsStatic || constructor.IsImplicitlyDeclared && constructor.Parameters.Length == 0)
            {
                continue;
            }

            if (HasAttribute(constructor, "System.Text.Json.Serialization.JsonConstructorAttribute"))
            {
                selected = constructor;
                break;
            }

            if (constructor.DeclaredAccessibility == Accessibility.Public)
            {
                publicCount++;
                singlePublic = constructor;
            }
        }

        selected ??= publicCount == 1 ? singlePublic : null;
        if (selected is null)
        {
            return null;
        }

        foreach (IParameterSymbol parameter in selected.Parameters)
        {
            if (string.Equals(parameter.Name, memberName, StringComparison.OrdinalIgnoreCase) &&
                SymbolEqualityComparer.Default.Equals(parameter.Type, memberType))
            {
                return parameter;
            }
        }

        return null;
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static string SafeName(INamedTypeSymbol type)
    {
        var builder = new StringBuilder();
        foreach (char character in type.ToDisplayString())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.ToString();
    }

    private static readonly SymbolDisplayFormat FullyQualified =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
            SymbolDisplayMiscellaneousOptions.ExpandNullable);

    internal static string BuilderInterfaceName => BuilderInterface;

    internal static string RegistryName => Registry;
}

/// <summary>The subset of <c>JsonIgnoreConditionEx</c> the generator needs to reason about.</summary>
internal enum JsonIgnoreConditionKind
{
    Never = 0,
    Always = 1,
    WhenWritingDefault = 2,
    WhenWritingNull = 3,
    WhenWriting = 4,
    WhenReading = 5,
    WhenReadingNull = 6,
    WhenReadingDefault = 7,
}

internal readonly struct JsonIgnoreConditionValue(JsonIgnoreConditionKind value)
{
    public JsonIgnoreConditionKind Value { get; } = value;

    public bool IsReadSide => Value is JsonIgnoreConditionKind.WhenReading
        or JsonIgnoreConditionKind.WhenReadingNull
        or JsonIgnoreConditionKind.WhenReadingDefault;

    public bool IsWriteSide => Value is JsonIgnoreConditionKind.WhenWriting
        or JsonIgnoreConditionKind.WhenWritingNull
        or JsonIgnoreConditionKind.WhenWritingDefault;
}

/// <summary>One annotated member, ready to be grouped by its containing type.</summary>
internal sealed class MemberAnalysis(
    string typeExpression,
    string safeName,
    List<MemberRegistration> registrations,
    List<DiagnosticInfo> diagnostics)
{
    public string TypeExpression { get; } = typeExpression;

    public string SafeName { get; } = safeName;

    public List<MemberRegistration> Registrations { get; } = registrations;

    public List<DiagnosticInfo> Diagnostics { get; } = diagnostics;
}
