# Scarlet.System.Text.Json.IgnoreCondition

`WhenReadingNull` and `WhenReadingDefault` for `System.Text.Json`, for both the reflection-based and
source-generated serializers, NativeAOT included. Standard ignore conditions use native `[JsonIgnore]`.

```csharp
public sealed class Settings
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string Endpoint { get; set; } = "https://localhost";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int RetryCount { get; set; } = 3;
}

var options = new JsonSerializerOptions
{
    TypeInfoResolver = new ExtendedIgnoreConditionResolver(),
};

var settings = JsonSerializer.Deserialize<Settings>("""{"Endpoint":null,"RetryCount":null}""", options);
// settings.Endpoint == "https://localhost"
// settings.RetryCount == 3          (without the library this line throws)
```

## Why

`System.Text.Json` supports ignoring an entire member when reading, but cannot conditionally skip an
incoming null or default value while preserving an initializer.

| Condition | net8 / net9 | net10 | configuration |
| --- | --- | --- | --- |
| `Never`, `Always`, `WhenWritingDefault`, `WhenWritingNull` | yes | yes | native `[JsonIgnore]` |
| `WhenWriting`, `WhenReading` | no | yes | native `[JsonIgnore]` |
| **`WhenReadingNull`** | **no** | **no** | `[JsonIgnoreEx(WhenReadingNull)]` |
| **`WhenReadingDefault`** | **no** | **no** | `[JsonIgnoreEx(WhenReadingDefault)]` |

That gap shows up as three long-standing issues:

- **[dotnet/runtime#90011]** — the `WhenReadingNull` proposal itself, still marked not ready for
  implementation.
- **[dotnet/runtime#62086]** — `JsonSerializerOptions.IgnoreNullValues` used to ignore nulls on read as
  well as write. Its replacement, `DefaultIgnoreCondition = WhenWritingNull`, deliberately does not, so
  `{"Values": null}` wipes a `List<string> Values { get; set; } = new()` initializer with no supported
  way to opt out.
- **[dotnet/runtime#74008]**, **[dotnet/runtime#83706]**, **[dotnet/runtime#30795]** — `{"Age": null}`
  against a non-nullable `int Age` throws instead of being ignored. Json.NET's
  `NullValueHandling.Ignore` ignores it, so migrations regress.

`JsonPropertyInfo.ShouldSerialize` has existed since .NET 7 for the write side; no `ShouldDeserialize`
counterpart was ever added. This library supplies the missing half through supported contract
customization.

## Install

```
dotnet add package Scarlet.System.Text.Json.IgnoreCondition
```

Targets **net10.0**. One runtime assembly, plus bundled analyzer and code-fix assemblies; the analyzer
assembly also carries a small source generator that registers custom value types for NativeAOT. Nothing
to configure and no MSBuild integration.

## Use

Annotate members, then set `ExtendedIgnoreConditionResolver` as the resolver.

```csharp
using Scarlet.System.Text.Json.IgnoreCondition;

// Source-generated, including NativeAOT — no reflection over your model
options.TypeInfoResolver = new ExtendedIgnoreConditionResolver(MyJsonContext.Default);

// Reflection-based
options.TypeInfoResolver = new ExtendedIgnoreConditionResolver();

// Composing with your own contract customizations: wrap whatever produced the metadata
var mine = new DefaultJsonTypeInfoResolver();
mine.Modifiers.Add(MyModifier);
options.TypeInfoResolver = new ExtendedIgnoreConditionResolver(mine);
```

Use `JsonIgnoreEx` for the extended read condition and native `JsonIgnore` for the write condition:

```csharp
[JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? Name { get; set; } = "initial";
```

The condition is required: a bare `[JsonIgnoreEx]` would read like native `[JsonIgnore]`, which excludes
the member entirely, so it does not compile. Only one `JsonIgnoreEx` is allowed per member.
The bundled analyzer warns when native `WhenReading` conflicts with it (`SJI0001`), or native `Always`
makes it ineffective (`SJI0002`). The fixes let you choose which conflicting rule to keep, or remove
the ineffective extended attribute. See [docs/analyzers.md](https://github.com/ScarletKuro/Scarlet.System.Text.Json.IgnoreCondition/blob/master/docs/analyzers.md).

### Records and constructor parameters

A member bound to a deserialization constructor parameter has no setter to intercept, so an incoming
null falls back to the parameter's own default value. Target the generated property explicitly:

```csharp
public sealed record Credentials(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string User = "anonymous");

// {"User":null} -> User == "anonymous"
```

This applies to types the serializer constructs through that constructor. A positional `record struct`
is deserialized through its parameterless constructor and settable properties, so a skipped null leaves
the member at `default`, not at the parameter's default value.

### Replacing `IgnoreNullValues` wholesale

The obsolete `IgnoreNullValues = true` skips nulls in both directions. Its replacement splits the two
halves: `DefaultIgnoreCondition` for writing, and this library's options-level default for reading.
Individual members opt out with native `[JsonIgnore(Condition = JsonIgnoreCondition.Never)]`. An explicit
`JsonIgnoreEx` still takes precedence over that opt-out.

```csharp
var options = new JsonSerializerOptions
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    TypeInfoResolver = new ExtendedIgnoreConditionResolver(
        MyJsonContext.Default,
        new ExtendedIgnoreConditionSettings
        {
            DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull,
        }),
};
```

Writing is identical to `IgnoreNullValues`. Reading skips every null `IgnoreNullValues` skipped, and three
more:

| Incoming `null` for | `IgnoreNullValues = true` | Replacement above |
| --- | --- | --- |
| reference type, collection, `Nullable<T>` | skipped | skipped |
| non-nullable value type, e.g. `int` | throws `JsonException` | skipped |
| constructor parameter of a non-nullable value type | throws `JsonException` | parameter default |
| member with a native write condition (`WhenWritingNull`, `WhenWritingDefault`, `WhenWriting`) | null assigned | skipped |
| member with native `Never` | null assigned | null assigned |

The fourth row is easy to miss: any native `[JsonIgnore(Condition = …)]`, even a write-only one, turns
`IgnoreNullValues` off for that member on the read side too. The library treats write conditions as
compatible with the read default; only `Never` opts out.

`IgnoreNullValues` and `DefaultIgnoreCondition` cannot both be set; System.Text.Json throws. The library
itself works alongside `IgnoreNullValues`, so a migration can keep it while members are annotated one at a
time. `[JsonIgnoreEx]` members then behave as documented, and the rest keep the old behaviour.

Under NativeAOT, custom enums and structs reached only by this default are not registered by the
generator. Set `RequireValueTypeRegistration = true` so JIT runs list them; see [NativeAOT](#nativeaot).

## How it works

The resolver walks `JsonTypeInfo.Properties` and reads each member's attributes from
`JsonPropertyInfo.AttributeProvider` — which the reflection-based resolver and the `System.Text.Json`
source generator both populate, so one implementation serves both. Then, per member, one of three
mechanisms, because the serializer reaches the three shapes of member differently:

| Member | Mechanism |
| --- | --- |
| Settable, type already accepts null (reference type or `Nullable<T>`) | `JsonPropertyInfo.Set` is wrapped, so the serializer's own null handling runs untouched and only the assignment is skipped. |
| Settable, non-nullable value type | The contract re-declares the member as `T?` with the same accessors and the serializer's own `Nullable<T>` converter. The null is then read rather than rejected, and the wrapped setter drops it. |
| Bound to a constructor parameter | The property's converter is intercepted and substitutes the parameter's default value for a null. |

Mistakes surface as a `NotSupportedException` naming the member and the reason, the first time the
contract is resolved — a read-side condition on a `required` member, on the extension data member, on a
member with no setter, two conditions on the same side, and so on. See
[docs/limitations.md](https://github.com/ScarletKuro/Scarlet.System.Text.Json.IgnoreCondition/blob/master/docs/limitations.md).

### NativeAOT

The value-type mechanisms need metadata for `Nullable<T>`, which is a generic instantiation over the
member type. The library roots that for every built-in value type, so nothing is created at run time for
the common cases. A **custom enum or struct** needs registering, and for members that carry
`[JsonIgnoreEx]` the bundled source generator does it for you: it emits a module initializer that calls
`ExtendedIgnoreConditionResolver.RegisterValueType<T>()` for each such type. There is nothing to configure.

The generator cannot see two cases, which you register yourself once at startup:

- a member reached only by `DefaultReadCondition`, with no `[JsonIgnoreEx]` of its own;
- a member whose type is a generic type parameter, closed over your struct elsewhere.

```csharp
ExtendedIgnoreConditionResolver.RegisterValueType<LogLevel>();
```

Miss one and the condition is refused when the contract is resolved, with a message naming exactly that
line, rather than surfacing later as a `JsonException` on a null payload. Under JIT the runtime fills the
gap silently, so an application heading for NativeAOT should turn on the strict check now and let its
ordinary tests find anything missing:

```csharp
new ExtendedIgnoreConditionSettings { RequireValueTypeRegistration = true }
```

Reference types, built-in value types and settable `Nullable<T>` members under `WhenReadingNull` never
need registering.

## Performance

Three findings, each of which reproduced across every run of
`benchmarks/Scarlet.System.Text.Json.IgnoreCondition.Benchmarks` (net10.0, in-process BenchmarkDotNet, a
four-member model through a source-generated context):

- **Unannotated types cost nothing.** Serializing a type with no conditions measures the same with the
  library installed as without it, because the resolver leaves contracts it has nothing to say about
  completely untouched — so they keep the source-generated serialization fast path.
- **Annotated types lose that fast path**, costing roughly 1.3–1.4× on serialization. Customizing a
  `JsonTypeInfo` sets the serializer's internal `IsCustomized` flag, which disables the generated fast
  path for that type. Every possible implementation of this feature pays that cost; the library confines
  it to the types that actually carry a condition.
- **Reading is not measurably slower.** Deserializing an annotated type measures the same as an
  unannotated one, and skipping a null is cheaper than assigning one (roughly 0.6× of a full read).

Absolute numbers are deliberately not quoted: the runs available were on a busy developer machine, with
standard deviations of 10–30%, which is enough to compare the pairs above but not to publish figures.
Measure on your own hardware with:

```
dotnet run --project benchmarks/Scarlet.System.Text.Json.IgnoreCondition.Benchmarks -c Release -- --filter '*'
```

## Build and validate

Requires the .NET 10 SDK. Tests use `xunit.v3.aot` and Microsoft.Testing.Platform, selected in
`global.json` for the .NET 10 test runner.

```sh
dotnet test --project tests/Scarlet.System.Text.Json.IgnoreCondition.Tests -c Release
dotnet test --project tests/Scarlet.System.Text.Json.IgnoreCondition.Analyzers.Tests -c Release
dotnet pack src/Scarlet.System.Text.Json.IgnoreCondition -c Release -o artifacts/packages
dotnet publish tests/Scarlet.System.Text.Json.IgnoreCondition.Tests -c Release -r win-x64 -p:NativeAotTests=true -o artifacts/aot
```

Run `artifacts/aot/Scarlet.System.Text.Json.IgnoreCondition.Tests.exe` after publishing. On Linux,
use `linux-x64` and run the executable without the `.exe` suffix. NativeAOT publishing requires the
platform's native compiler toolchain.

The managed run covers reflection and both source-generated resolver modes. `NativeAotTests=true`
compiles the same behavioral tests for both source-generated modes, excludes reflection-only tests,
and disables serializer reflection. The published native binary also asserts that dynamic code is
unavailable. Referencing the AOT test package alone does not replace publishing and running it.

The CI workflow runs the managed tests, packs the library, and publishes and executes the NativeAOT
tests on Windows and Linux. Package metadata, including the repository URL, is declared in
`Directory.Build.props`, so a local pack produces the same package as CI.

## Licence

MIT.

[dotnet/runtime#90011]: https://github.com/dotnet/runtime/issues/90011
[dotnet/runtime#62086]: https://github.com/dotnet/runtime/issues/62086
[dotnet/runtime#74008]: https://github.com/dotnet/runtime/issues/74008
[dotnet/runtime#83706]: https://github.com/dotnet/runtime/issues/83706
[dotnet/runtime#30795]: https://github.com/dotnet/runtime/issues/30795
