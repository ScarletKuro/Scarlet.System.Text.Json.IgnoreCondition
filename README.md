# Scarlet.System.Text.Json.IgnoreCondition
[![Nuget](https://img.shields.io/nuget/v/Scarlet.System.Text.Json.IgnoreCondition?color=ff4081&logo=nuget)](https://www.nuget.org/packages/Scarlet.System.Text.Json.IgnoreCondition/)
[![Nuget](https://img.shields.io/nuget/dt/Scarlet.System.Text.Json.IgnoreCondition?color=ff4081&label=nuget%20downloads&logo=nuget)](https://www.nuget.org/packages/Scarlet.System.Text.Json.IgnoreCondition/)
[![GitHub](https://img.shields.io/github/license/ScarletKuro/Scarlet.System.Text.Json.IgnoreCondition?color=594ae2&logo=github)](https://github.com/ScarletKuro/Scarlet.System.Text.Json.IgnoreCondition/blob/master/LICENSE)
[![codecov](https://codecov.io/github/ScarletKuro/Scarlet.System.Text.Json.IgnoreCondition/graph/badge.svg?token=UF5Q4EFWDG)](https://codecov.io/github/ScarletKuro/Scarlet.System.Text.Json.IgnoreCondition)

Skip `null` (or default) values when deserializing with `System.Text.Json`, so your properties keep their
initial values. Adds the missing `WhenReadingNull` and `WhenReadingDefault` ignore conditions, and works with
reflection, source generation and NativeAOT.

```csharp
public sealed class Settings
{
    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public string Endpoint { get; set; } = "https://localhost";

    [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
    public int RetryCount { get; set; } = 3;
}

var options = new JsonSerializerOptions { TypeInfoResolver = new ExtendedIgnoreConditionResolver() };

var settings = JsonSerializer.Deserialize<Settings>("""{"Endpoint":null,"RetryCount":null}""", options);
// settings.Endpoint   == "https://localhost"
// settings.RetryCount == 3    (plain System.Text.Json throws here)
```

## Why

Out of the box, `System.Text.Json` can ignore a member entirely, but it cannot skip just an incoming `null`.
That causes a few well-known problems:

- **A `null` wipes your initializer.** `{"Values": null}` replaces `List<string> Values { get; set; } = [];`
  with `null` ([dotnet/runtime#62086]).
- **A `null` for a non-nullable value type throws.** `{"Age": null}` against `int Age` is a `JsonException`
  ([dotnet/runtime#74008], [#83706][dotnet/runtime#83706], [#30795][dotnet/runtime#30795]).
- **Migrating from Newtonsoft.Json regresses.** `NullValueHandling.Ignore` skipped those nulls; there is no
  built-in equivalent ([dotnet/runtime#90011]).

## Getting started

```
dotnet add package Scarlet.System.Text.Json.IgnoreCondition
```

Requires **.NET 10**. Annotate your members, then use `ExtendedIgnoreConditionResolver` as the
`TypeInfoResolver`:

```csharp
using Scarlet.System.Text.Json.IgnoreCondition;

var options = new JsonSerializerOptions
{
    // Source generation (recommended, NativeAOT-compatible)
    TypeInfoResolver = new ExtendedIgnoreConditionResolver(MyJsonContext.Default),

    // or reflection:
    // TypeInfoResolver = new ExtendedIgnoreConditionResolver(),
};
```

Already customizing contracts? Wrap whatever produces your metadata, and your modifiers keep working:

```csharp
var resolver = new DefaultJsonTypeInfoResolver();
resolver.Modifiers.Add(MyModifier);
options.TypeInfoResolver = new ExtendedIgnoreConditionResolver(resolver);
```

## The conditions

| Condition | Skips an incoming… | Example: `int Count = 3` receives |
| --- | --- | --- |
| `WhenReadingNull` | `null` | `null` → stays `3`; `0` → becomes `0` |
| `WhenReadingDefault` | `null` or the type's default value | `null` → stays `3`; `0` → stays `3` |

The default is that of the declared type: for `int?` it is `null`, so `WhenReadingDefault` on an `int?`
skips `null` but assigns `0`.

These conditions only affect reading. For writing, keep using the native `[JsonIgnore]`, and combine the
two freely:

```csharp
[JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]                 // reading: skip null
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]         // writing: omit null
public string? Currency { get; set; } = "EUR";
```

The condition is always spelled out: a bare `[JsonIgnoreEx]` would look like `[JsonIgnore]`, which does
something very different, so it does not compile.

### Records and constructor parameters

For a positional record, put the attribute on the generated property with `property:`. An incoming `null`
then falls back to the parameter's default value:

```csharp
public sealed record Credentials(
    [property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string User = "anonymous");

// {"User":null} -> User == "anonymous"
```

A positional `record struct` is deserialized through its parameterless constructor, so there a skipped
`null` leaves the member at `default`.

### Applying a condition to every member

Instead of annotating each member, set a default for all of them:

```csharp
var options = new JsonSerializerOptions
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,   // writing: omit nulls
    TypeInfoResolver = new ExtendedIgnoreConditionResolver(
        MyJsonContext.Default,
        new ExtendedIgnoreConditionSettings
        {
            DefaultReadCondition = JsonIgnoreConditionEx.WhenReadingNull,   // reading: skip nulls
        }),
};
```

A single member opts out with `[JsonIgnore(Condition = JsonIgnoreCondition.Never)]`, and an explicit
`[JsonIgnoreEx]` always wins over the default. Members the default cannot apply to, such as `required` or
read-only ones, are left alone.

## Migrating from Newtonsoft.Json or `IgnoreNullValues`

| You have | Use |
| --- | --- |
| `[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]` | `[JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]` plus `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` |
| `JsonSerializerSettings.NullValueHandling = NullValueHandling.Ignore` | [A default for every member](#applying-a-condition-to-every-member) |
| `JsonSerializerOptions.IgnoreNullValues = true` (obsolete) | [A default for every member](#applying-a-condition-to-every-member) |

The options-level replacement writes exactly what `IgnoreNullValues` wrote, and skips every `null` it
skipped when reading, plus a few it never could:

| Incoming `null` for | `IgnoreNullValues = true` | This library |
| --- | --- | --- |
| reference type, collection, `Nullable<T>` | skipped | skipped |
| non-nullable value type, e.g. `int` | throws `JsonException` | skipped |
| constructor parameter of a non-nullable value type | throws `JsonException` | parameter default |
| member with a native write condition, e.g. `[JsonIgnore(Condition = WhenWritingNull)]` | `null` assigned | skipped |
| member with `[JsonIgnore(Condition = Never)]` | `null` assigned | `null` assigned |

You can also migrate gradually: the resolver works alongside `IgnoreNullValues`, so members can be
annotated one at a time before switching the options over.

## NativeAOT

It works out of the box. Custom enums and structs need a small piece of registration under NativeAOT, and
for members marked `[JsonIgnoreEx]` the package's source generator does it for you.

Register a type yourself, once at startup, only when the generator cannot see it:

- it is used only through `DefaultReadCondition`, by a member without `[JsonIgnoreEx]`;
- it reaches a generic model through a type parameter, e.g. `MyModel<LogLevel>`.

```csharp
ExtendedIgnoreConditionResolver.RegisterValueType<LogLevel>();
```

Under JIT a missing registration goes unnoticed, so if you plan to publish with NativeAOT, turn on the
strict check now. Your ordinary tests then report anything missing, with the exact line to add:

```csharp
new ExtendedIgnoreConditionSettings { RequireValueTypeRegistration = true }
```

Built-in types (`int`, `Guid`, `DateTime`, `string` and so on) never need registering.

## Analyzer

The package includes an analyzer with code fixes for attribute combinations that cannot work:

| Rule | Reported when |
| --- | --- |
| `SJI0001` | `[JsonIgnore(Condition = WhenReading)]` conflicts with `[JsonIgnoreEx]` on the same member |
| `SJI0002` | `[JsonIgnore]` already ignores the member, so `[JsonIgnoreEx]` has no effect |

See [the analyzer documentation][analyzers] for details.

## Good to know

- **Mistakes fail fast.** A condition that cannot be applied, for example on a `required` member or one
  with no setter, throws a `NotSupportedException` naming the member and the reason when the type is first
  used, instead of being silently ignored. The full list is in [Limitations][limitations].
- **Performance.** Types without conditions are untouched and cost nothing. Annotated types lose the
  source generator's serialization fast path, which costs roughly 1.3–1.4× when *writing* them; that is
  inherent to customizing a contract. Reading is not measurably slower.
- **How it works.** The resolver uses `System.Text.Json`'s supported contract customization API to skip
  the assignment of an incoming `null` or default value. No private reflection is involved.

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/ScarletKuro/Scarlet.System.Text.Json.IgnoreCondition/blob/master/LICENSE) file for details.

[dotnet/runtime#90011]: https://github.com/dotnet/runtime/issues/90011
[dotnet/runtime#62086]: https://github.com/dotnet/runtime/issues/62086
[dotnet/runtime#74008]: https://github.com/dotnet/runtime/issues/74008
[dotnet/runtime#83706]: https://github.com/dotnet/runtime/issues/83706
[dotnet/runtime#30795]: https://github.com/dotnet/runtime/issues/30795
[analyzers]: https://github.com/ScarletKuro/Scarlet.System.Text.Json.IgnoreCondition/blob/master/docs/analyzers.md
[limitations]: https://github.com/ScarletKuro/Scarlet.System.Text.Json.IgnoreCondition/blob/master/docs/limitations.md
