using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Scarlet.Text.Json.Serialization;

namespace Scarlet.Text.Json.Benchmarks;

internal static class Program
{
    private static void Main(string[] args) => BenchmarkSwitcher
        .FromAssembly(typeof(Program).Assembly)
        .Run(args);
}

/// <summary>
/// Quantifies the two costs worth knowing about: the per-member overhead of an intercepted read, and
/// the loss of the source-generated serialization fast path, which any contract customization
/// disables. The unannotated benchmarks are the control: they must show no regression, because the
/// modifier leaves contracts it has nothing to say about completely untouched.
/// </summary>
[MemoryDiagnoser]
[InProcess]
public class IgnoreConditionBenchmarks
{
    private const string AnnotatedJson = """{"Name":"given","Values":["b"],"RetryCount":9,"Level":2}""";
    private const string AnnotatedNullJson = """{"Name":null,"Values":null,"RetryCount":null,"Level":null}""";
    private const string PlainJson = """{"Name":"given","Values":["b"],"RetryCount":9,"Level":2}""";

    private JsonSerializerOptions _baseline = null!;
    private JsonSerializerOptions _extended = null!;
    private AnnotatedModel _annotated = null!;
    private PlainModel _plain = null!;

    [GlobalSetup]
    public void Setup()
    {
        _baseline = new JsonSerializerOptions { TypeInfoResolver = BenchmarkContext.Default };
        _extended = new JsonSerializerOptions
        {
            TypeInfoResolver = new ExtendedIgnoreConditionResolver(BenchmarkContext.Default),
        };

        _annotated = JsonSerializer.Deserialize<AnnotatedModel>(AnnotatedJson, _baseline)!;
        _plain = JsonSerializer.Deserialize<PlainModel>(PlainJson, _baseline)!;
    }

    [Benchmark(Baseline = true, Description = "serialize unannotated, no library")]
    public string SerializeUnannotatedBaseline() => JsonSerializer.Serialize(_plain, _baseline);

    [Benchmark(Description = "serialize unannotated, library installed")]
    public string SerializeUnannotatedExtended() => JsonSerializer.Serialize(_plain, _extended);

    [Benchmark(Description = "serialize annotated, no library")]
    public string SerializeAnnotatedBaseline() => JsonSerializer.Serialize(_annotated, _baseline);

    [Benchmark(Description = "serialize annotated, library installed (fast path disabled)")]
    public string SerializeAnnotatedExtended() => JsonSerializer.Serialize(_annotated, _extended);

    [Benchmark(Description = "deserialize annotated with values")]
    public AnnotatedModel DeserializeValues() =>
        JsonSerializer.Deserialize<AnnotatedModel>(AnnotatedJson, _extended)!;

    [Benchmark(Description = "deserialize annotated with nulls (all skipped)")]
    public AnnotatedModel DeserializeNulls() =>
        JsonSerializer.Deserialize<AnnotatedModel>(AnnotatedNullJson, _extended)!;

    [Benchmark(Description = "deserialize unannotated, library installed")]
    public PlainModel DeserializeUnannotated() =>
        JsonSerializer.Deserialize<PlainModel>(PlainJson, _extended)!;
}

public enum Level
{
    Low,
    Medium,
    High,
}

public sealed class AnnotatedModel
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string? Name { get; set; } = "initial";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public List<string> Values { get; set; } = ["a"];

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int RetryCount { get; set; } = 3;

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public Level Level { get; set; } = Level.Medium;
}

public sealed class PlainModel
{
    public string? Name { get; set; } = "initial";

    public List<string> Values { get; set; } = ["a"];

    public int RetryCount { get; set; } = 3;

    public Level Level { get; set; } = Level.Medium;
}

[JsonSerializable(typeof(AnnotatedModel))]
[JsonSerializable(typeof(PlainModel))]
internal sealed partial class BenchmarkContext : JsonSerializerContext;
