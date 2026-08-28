using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Scarlet.System.Text.Json.IgnoreCondition.Internal;

/// <summary>
/// Decides whether a member type is handled by a "value" converter, i.e. one that reads and writes a
/// single JSON token and therefore cannot suspend mid-value.
/// </summary>
/// <remarks>
/// <para>
/// Forwarding a read to <see cref="global::System.Text.Json.Serialization.JsonConverter{T}.Read"/>
/// is only faithful for such types. For an object or collection converter that entry point spins up a
/// fresh internal read stack, which would silently drop reference handling, polymorphic metadata and
/// the ability to resume a partially buffered read. Rather than be quietly wrong, conditions that
/// need converter interception are refused for those types.
/// </para>
/// <para>
/// The classification is deliberately static, not a
/// <c>JsonSerializerOptions.GetTypeInfo</c> query: the check runs from inside a contract modifier,
/// where resolving another type's contract can re-enter the resolver. It also lets the analyzer make
/// the identical judgement at compile time.
/// </para>
/// </remarks>
internal static class ConverterBasedTypes
{
    private static readonly HashSet<Type> KnownValueTypes =
    [
        typeof(bool), typeof(byte), typeof(sbyte), typeof(char), typeof(decimal), typeof(double),
        typeof(float), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long),
        typeof(ulong), typeof(Half), typeof(Int128), typeof(UInt128), typeof(BigInteger),
        typeof(string), typeof(object),
        typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly),
        typeof(TimeSpan), typeof(Guid), typeof(Uri), typeof(Version),
        typeof(JsonElement), typeof(JsonDocument), typeof(JsonNode), typeof(JsonValue),
        typeof(byte[]), typeof(Memory<byte>), typeof(ReadOnlyMemory<byte>),
    ];

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> is known to be serialized by a
    /// single-token converter, or when the member declares its own converter and the caller has
    /// therefore taken responsibility for it.
    /// </summary>
    public static bool IsConverterBased(Type type, bool hasMemberLevelConverter)
    {
        if (hasMemberLevelConverter)
        {
            // The member author chose the converter; assume they know its strategy.
            return true;
        }

        Type effective = Nullable.GetUnderlyingType(type) ?? type;
        return effective.IsEnum || KnownValueTypes.Contains(effective);
    }
}
