using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Scarlet.Text.Json.SourceGeneration;

/// <summary>
/// The compile-time mirror of the runtime's <c>ConverterBasedTypes</c> and numeric classification, so
/// the analyzer warns about exactly the cases the runtime will refuse.
/// </summary>
internal static class TypeClassification
{
    private static readonly HashSet<string> ConverterBasedNames = new(
        [
            "System.String", "System.Object", "System.Uri", "System.Version",
            "System.DateTime", "System.DateTimeOffset", "System.DateOnly", "System.TimeOnly",
            "System.TimeSpan", "System.Guid", "System.Half", "System.Int128", "System.UInt128",
            "System.Numerics.BigInteger",
            "System.Text.Json.JsonElement", "System.Text.Json.JsonDocument",
            "System.Text.Json.Nodes.JsonNode", "System.Text.Json.Nodes.JsonValue",
        ]);

    private static readonly HashSet<SpecialType> NumericSpecialTypes =
        [
            SpecialType.System_Byte, SpecialType.System_SByte, SpecialType.System_Int16,
            SpecialType.System_UInt16, SpecialType.System_Int32, SpecialType.System_UInt32,
            SpecialType.System_Int64, SpecialType.System_UInt64, SpecialType.System_Single,
            SpecialType.System_Double, SpecialType.System_Decimal,
        ];

    public static bool IsNonNullableValueType(ITypeSymbol type) =>
        type.IsValueType && !IsNullableOfT(type) && type.TypeKind != TypeKind.TypeParameter;

    public static bool IsConverterBased(ITypeSymbol type, bool hasMemberLevelConverter)
    {
        if (hasMemberLevelConverter)
        {
            return true;
        }

        ITypeSymbol effective = Unwrap(type);

        if (effective.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (effective.SpecialType is not SpecialType.None)
        {
            return effective.SpecialType is not (SpecialType.System_Array or SpecialType.System_Collections_IEnumerable);
        }

        if (effective is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
        {
            return true;
        }

        return ConverterBasedNames.Contains(effective.ToDisplayString());
    }

    public static bool IsNumeric(ITypeSymbol type)
    {
        ITypeSymbol effective = Unwrap(type);
        return NumericSpecialTypes.Contains(effective.SpecialType) ||
               effective.ToDisplayString() is "System.Half" or "System.Int128" or "System.UInt128";
    }

    private static bool IsNullableOfT(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

    private static ITypeSymbol Unwrap(ITypeSymbol type) =>
        IsNullableOfT(type) && type is INamedTypeSymbol named ? named.TypeArguments[0] : type;
}
