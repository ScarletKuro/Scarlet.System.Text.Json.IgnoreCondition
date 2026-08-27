using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Scarlet.Text.Json.Serialization;

namespace Scarlet.Text.Json.AotSample;

/// <summary>
/// Published with <c>PublishAot</c> and with reflection-based serialization switched off, so the build
/// fails on any trim or AOT warning and the run fails if anything needs runtime code generation. Exit
/// code 0 means the library's source-generated path is genuinely AOT-clean.
/// </summary>
internal static class Program
{
    private static int Main()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = SampleContext.Default.WithExtendedIgnoreConditions(),
        };

        var settings = (JsonTypeInfo<Settings>)options.GetTypeInfo(typeof(Settings));
        var credentials = (JsonTypeInfo<Credentials>)options.GetTypeInfo(typeof(Credentials));

        int failures = 0;

        failures += Check(
            "reference member keeps its initial value",
            JsonSerializer.Deserialize("""{"Endpoint":null}""", settings)!.Endpoint,
            "https://localhost");

        failures += Check(
            "collection member keeps its initializer",
            JsonSerializer.Deserialize("""{"Tags":null}""", settings)!.Tags.Count,
            1);

        failures += Check(
            "non-nullable value member keeps its initial value",
            JsonSerializer.Deserialize("""{"RetryCount":null}""", settings)!.RetryCount,
            3);

        failures += Check(
            "non-nullable value member still reads a real value",
            JsonSerializer.Deserialize("""{"RetryCount":9}""", settings)!.RetryCount,
            9);

        failures += Check(
            "enum member keeps its initial value",
            JsonSerializer.Deserialize("""{"Level":null}""", settings)!.Level,
            LogLevel.Warning);

        failures += Check(
            "constructor parameter falls back to its default",
            JsonSerializer.Deserialize("""{"User":null,"Attempts":null}""", credentials)!.User,
            "anonymous");

        failures += Check(
            "constructor parameter of a value type falls back to its default",
            JsonSerializer.Deserialize("""{"Attempts":null}""", credentials)!.Attempts,
            5);

        failures += Check(
            "write-side condition still omits nulls",
            JsonSerializer.Serialize(new Settings { Endpoint = null }, settings).Contains("Endpoint"),
            false);

        Console.WriteLine(failures == 0
            ? "All AOT checks passed."
            : $"{failures} AOT check(s) failed.");

        return failures;
    }

    private static int Check<T>(string description, T actual, T expected)
    {
        bool passed = EqualityComparer<T>.Default.Equals(actual, expected);
        Console.WriteLine($"{(passed ? "pass" : "FAIL")}: {description} (expected {expected}, got {actual})");
        return passed ? 0 : 1;
    }
}

public enum LogLevel
{
    Debug,
    Information,
    Warning,
    Error,
}

public sealed class Settings
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenWritingNull)]
    public string? Endpoint { get; set; } = "https://localhost";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public List<string> Tags { get; set; } = ["default"];

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int RetryCount { get; set; } = 3;

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public LogLevel Level { get; set; } = LogLevel.Warning;
}

public sealed record Credentials(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string User = "anonymous",
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] int Attempts = 5);

[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(Credentials))]
internal sealed partial class SampleContext : JsonSerializerContext;
