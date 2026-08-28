# Scarlet.System.Text.Json.IgnoreCondition

`JsonIgnoreCondition.WhenReadingNull` for `System.Text.Json` — plus the rest of the read/write ignore
matrix — for both the reflection-based and the source-generated serializer, NativeAOT included.

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

`System.Text.Json` can ignore a member when *writing*, never when *reading*.

| Condition | net8 / net9 | net10 | this library |
| --- | --- | --- | --- |
| `Never`, `Always`, `WhenWritingDefault`, `WhenWritingNull` | yes | yes | yes |
| `WhenWriting`, `WhenReading` | no | yes | yes |
| **`WhenReadingNull`** | **no** | **no** | **yes** |
| **`WhenReadingDefault`** | **no** | **no** | **yes** |

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

Targets **net10.0**. No source generator, no analyzer, no MSBuild integration — one assembly.

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

The attribute may be applied twice to combine one read-side condition with one write-side condition:

```csharp
[JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
[JsonIgnoreEx(JsonIgnoreConditionEx.WhenWritingNull)]
public string? Name { get; set; } = "initial";
```

### Records and constructor parameters

A member bound to a deserialization constructor parameter has no setter to intercept, so an incoming
null falls back to the parameter's own default value. Target the generated property explicitly:

```csharp
public sealed record Credentials(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string User = "anonymous");

// {"User":null} -> User == "anonymous"
```

### Replacing `IgnoreNullValues` wholesale

To restore the read-side behaviour of the obsolete `IgnoreNullValues` across every type, set an
options-level default. Individual members opt out with `Never`.

```csharp
options.TypeInfoResolver = new ExtendedIgnoreConditionResolver(
    MyJsonContext.Default,
    new ExtendedIgnoreConditionSettings
    {
        DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull,
    });
```

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
[docs/limitations.md](docs/limitations.md).

### NativeAOT

The value-type mechanisms need metadata for `Nullable<T>`, which is a generic instantiation over the
member type. The library roots that for every built-in value type, so nothing is created at run time for
the common cases. For a **custom enum or struct** under NativeAOT, register it once at startup:

```csharp
ExtendedIgnoreConditionResolver.RegisterValueType<LogLevel>();
```

Skip it and the condition is refused with a message that tells you to add exactly that line. Everything
else — reference types, `Nullable<T>`, collections, constructor parameters of reference type — needs no
registration.

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

## Licence

MIT.

[dotnet/runtime#90011]: https://github.com/dotnet/runtime/issues/90011
[dotnet/runtime#62086]: https://github.com/dotnet/runtime/issues/62086
[dotnet/runtime#74008]: https://github.com/dotnet/runtime/issues/74008
[dotnet/runtime#83706]: https://github.com/dotnet/runtime/issues/83706
[dotnet/runtime#30795]: https://github.com/dotnet/runtime/issues/30795
[dotnet/runtime#66490]: https://github.com/dotnet/runtime/issues/66490
