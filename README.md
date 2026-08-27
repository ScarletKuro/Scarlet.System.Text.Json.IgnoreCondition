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

var options = new JsonSerializerOptions().AddExtendedIgnoreConditions();

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

One package reference gives you the runtime library and the Roslyn generator that registers your
annotated types at compile time. Targets **net10.0**.

## Use

Annotate members, then install the contract customization once per `JsonSerializerOptions`.

```csharp
using Scarlet.Text.Json.Serialization;

// Reflection-based serialization
var options = new JsonSerializerOptions().AddExtendedIgnoreConditions();

// Source-generated / NativeAOT — no reflection anywhere on this path
var options = new JsonSerializerOptions
{
    TypeInfoResolver = MyContext.Default.WithExtendedIgnoreConditions(),
};

// Alongside other contract customizations
var resolver = new DefaultJsonTypeInfoResolver();
resolver.Modifiers.Add(ScarletJsonIgnoreConditionExtensions.CreateModifier());
```

The attribute may be applied twice to combine one read-side condition with one write-side condition:

```csharp
[JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
[JsonIgnoreEx(JsonIgnoreConditionEx.WhenWritingNull)]
public string? Name { get; set; } = "initial";
```

### Records and constructor parameters

A member bound to a deserialization constructor parameter has no setter to intercept, so an incoming
null falls back to the parameter's own default value:

```csharp
public sealed record Credentials(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string User = "anonymous");

// {"User":null} -> User == "anonymous"
```

Writing the attribute directly on the positional parameter (without `property:`) works too.

### Replacing `IgnoreNullValues` wholesale

To restore the read-side behaviour of the obsolete `IgnoreNullValues` across every type, set an
options-level default. Individual members opt out with `Never`.

```csharp
var options = new JsonSerializerOptions().AddExtendedIgnoreConditions(
    new ExtendedIgnoreConditionSettings
    {
        DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull,
    });
```

## How it works

Three mechanisms, because the serializer reaches the three shapes of member differently. All of them
are ordinary contract customization — no reflection into serializer internals.

| Member | Mechanism |
| --- | --- |
| Settable, type already accepts null (reference type or `Nullable<T>`) | `JsonPropertyInfo.Set` is wrapped, so the serializer's own null handling runs untouched and only the assignment is skipped. |
| Settable, non-nullable value type | The contract re-declares the member as `T?` with the same accessors and the serializer's own `Nullable<T>` converter. The null is then read rather than rejected, and the wrapped setter drops it. |
| Bound to a constructor parameter | The property's converter is intercepted and substitutes the parameter's default value for a null. |

The Roslyn generator emits one registration per annotated type and calls it from a module
initializer, passing each member type as a generic argument. That is what keeps the whole path free of
`MakeGenericType`, and therefore NativeAOT-safe. Assemblies that disable the generator fall back to
reading the attributes by reflection.

Misuse is caught at compile time by eleven analyzer rules (`SJIC001`–`SJIC011`) — a read-side
condition on a `required` member, `[JsonIgnoreEx]` next to `[JsonIgnore]`, two conditions on the same
side, and so on.

## Performance

`net10.0`, in-process BenchmarkDotNet, a four-member model through a source-generated context:

| | ns | vs. baseline |
| --- | --- | --- |
| serialize unannotated type, library not installed | 255.9 | 1.00 |
| serialize unannotated type, library installed | 256.1 | 1.00 |
| serialize annotated type, library not installed | 254.2 | 0.99 |
| serialize annotated type, library installed | 359.4 | 1.40 |
| deserialize annotated type, values present | 591.4 | — |
| deserialize annotated type, all nulls skipped | 355.6 | — |
| deserialize unannotated type, library installed | 599.4 | — |

Two things to read from this:

- **Unannotated types cost nothing.** The modifier leaves contracts it has nothing to say about
  completely untouched, so they keep the source-generated serialization fast path.
- **Annotated types lose that fast path** (~1.4× on serialization). Customizing a `JsonTypeInfo` sets
  the serializer's internal `IsCustomized` flag, which disables the generated fast path for that type.
  Every possible implementation of this feature pays that cost; the library confines it to the types
  that actually carry a condition.
- Reading is not measurably slower — skipping a null is in fact cheaper than assigning one.

## Limitations

See [docs/limitations.md](docs/limitations.md).

## Licence

MIT.

[dotnet/runtime#90011]: https://github.com/dotnet/runtime/issues/90011
[dotnet/runtime#62086]: https://github.com/dotnet/runtime/issues/62086
[dotnet/runtime#74008]: https://github.com/dotnet/runtime/issues/74008
[dotnet/runtime#83706]: https://github.com/dotnet/runtime/issues/83706
[dotnet/runtime#30795]: https://github.com/dotnet/runtime/issues/30795
[dotnet/runtime#66490]: https://github.com/dotnet/runtime/issues/66490
