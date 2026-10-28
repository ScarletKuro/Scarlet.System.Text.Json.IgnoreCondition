using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Scarlet.System.Text.Json.IgnoreCondition.Analyzers;

public sealed partial class IgnoreConditionAnalyzer
{
    public const string ConflictId = "SJI0001";
    public const string IneffectiveId = "SJI0002";

    public static readonly DiagnosticDescriptor Conflict = new(
        ConflictId, "Conflicting read ignore conditions",
        "Native WhenReading conflicts with JsonIgnoreEx({1}) on member '{0}'",
        "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor Ineffective = new(
        IneffectiveId, "Extended ignore condition has no effect",
        "JsonIgnoreEx on '{0}' has no effect because JsonIgnore always ignores the member",
        "Usage", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Conflict, Ineffective);
}
