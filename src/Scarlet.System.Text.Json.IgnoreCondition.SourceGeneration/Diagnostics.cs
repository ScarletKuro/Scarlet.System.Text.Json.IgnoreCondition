using Microsoft.CodeAnalysis;

namespace Scarlet.Text.Json.SourceGeneration;

/// <summary>The diagnostics reported for misuse of <c>[JsonIgnoreEx]</c>.</summary>
internal static class Diagnostics
{
    private const string Category = "Scarlet.Json";

    public static readonly DiagnosticDescriptor RequiredMember = Error(
        "SJIC001",
        "Read-side ignore condition on a required member",
        "'{0}.{1}' is required, so the serializer must observe a value for it; a read-side ignore condition cannot be applied");

    public static readonly DiagnosticDescriptor NoSetter = Error(
        "SJIC002",
        "Read-side ignore condition on a member that cannot be assigned",
        "'{0}.{1}' has no setter and is not bound to a deserialization constructor parameter, so there is no assignment for a read-side condition to skip");

    public static readonly DiagnosticDescriptor CombinedWithJsonIgnore = Error(
        "SJIC003",
        "[JsonIgnoreEx] combined with [JsonIgnore]",
        "'{0}.{1}' carries both [JsonIgnore] and [JsonIgnoreEx]; [JsonIgnore] removes the member from the contract, leaving nothing for the extended condition to act on");

    public static readonly DiagnosticDescriptor DuplicateCondition = Error(
        "SJIC004",
        "Two ignore conditions on the same side",
        "'{0}.{1}' declares more than one {2}-side ignore condition");

    public static readonly DiagnosticDescriptor NotConverterBased = Warning(
        "SJIC005",
        "Ignore condition needs converter interception on a type that does not support it",
        "'{0}.{1}' is bound to a deserialization constructor parameter of a type that is not serialized by a single-token converter; the condition will be refused at run time because intercepting it would lose reference handling, polymorphic metadata and resumable reads");

    public static readonly DiagnosticDescriptor NumberHandlingConflict = Warning(
        "SJIC006",
        "Ignore condition conflicts with JsonNumberHandling",
        "'{0}.{1}' is a numeric deserialization constructor parameter with a non-default JsonNumberHandling; the condition will be refused at run time because intercepting the converter would drop that handling");

    public static readonly DiagnosticDescriptor ExtensionData = Warning(
        "SJIC007",
        "[JsonIgnoreEx] on extension data",
        "'{0}.{1}' is the extension data member, which the extended ignore conditions do not apply to");

    public static readonly DiagnosticDescriptor NullSilentlySkipped = Info(
        "SJIC008",
        "Explicit JSON null will be silently skipped",
        "'{0}.{1}' is a non-nullable value type, so an explicit JSON null will be skipped instead of rejected");

    public static readonly DiagnosticDescriptor ResolverNotInstalled = Info(
        "SJIC009",
        "Extended ignore conditions are never installed",
        "This compilation uses [JsonIgnoreEx] but never calls AddExtendedIgnoreConditions or WithExtendedIgnoreConditions, so the conditions will have no effect");

    public static readonly DiagnosticDescriptor GenericTypeNotRegistered = Warning(
        "SJIC010",
        "Generic type is not registered at compile time",
        "'{0}' is generic, so its ignore conditions cannot be registered at compile time and will be discovered by reflection; that path is not NativeAOT-safe");

    public static readonly DiagnosticDescriptor TypeNotAccessible = Warning(
        "SJIC011",
        "Type is not accessible to the generated registry",
        "'{0}' is not accessible from assembly scope, so its ignore conditions cannot be registered at compile time and will be discovered by reflection; that path is not NativeAOT-safe");

    private static DiagnosticDescriptor Error(string id, string title, string format) =>
        new(id, title, format, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static DiagnosticDescriptor Warning(string id, string title, string format) =>
        new(id, title, format, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static DiagnosticDescriptor Info(string id, string title, string format) =>
        new(id, title, format, Category, DiagnosticSeverity.Info, isEnabledByDefault: true);

    public static DiagnosticDescriptor? ById(string id) => id switch
    {
        "SJIC001" => RequiredMember,
        "SJIC002" => NoSetter,
        "SJIC003" => CombinedWithJsonIgnore,
        "SJIC004" => DuplicateCondition,
        "SJIC005" => NotConverterBased,
        "SJIC006" => NumberHandlingConflict,
        "SJIC007" => ExtensionData,
        "SJIC008" => NullSilentlySkipped,
        "SJIC009" => ResolverNotInstalled,
        "SJIC010" => GenericTypeNotRegistered,
        "SJIC011" => TypeNotAccessible,
        _ => null,
    };
}
