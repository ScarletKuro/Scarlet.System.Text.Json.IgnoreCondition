# Limitations

Each of these fails loudly: a `NotSupportedException` naming the member and the reason, thrown the first
time `System.Text.Json` resolves the contract. Set
`ExtendedIgnoreConditionSettings.UnsupportedMemberBehavior = UnsupportedMemberBehavior.Skip` to leave the
built-in behaviour in place instead of throwing.

## The source-generated fast path is disabled for annotated types

`JsonTypeInfo.DetermineIsCompatibleWithCurrentOptions` returns false as soon as a contract has been
customized, which switches off the serialization fast path the `System.Text.Json` generator emits.
Measured at roughly 1.3–1.4× on serialization.

This is unavoidable for any implementation of this feature — the whole point is to customize the
contract. What the library does control is the blast radius: a type with no `[JsonIgnoreEx]` and no
applicable options-level default is not touched at all, so it keeps its fast path.

## Read-side conditions cannot apply to some members

| Member | Why |
| --- | --- |
| `required` | The serializer must observe a value for it, so there is nothing to skip. |
| No setter and not constructor-bound | There is no assignment to skip. |
| `[JsonExtensionData]` | Extension data is not a normal member. |
| `init`-only, with source generation | The `System.Text.Json` source generator assigns init-only members through an object initializer, so the setter the condition relies on is never called. A member absent from the JSON loses its initializer too, with or without this library. Use `set`, a constructor parameter, or the reflection-based resolver, which calls the init accessor like any setter. |
| Also carries `[JsonIgnore]` | `[JsonIgnore]` wins. The serializer keeps the contract entry but strips both accessors, so the extended condition has nothing to act on and is skipped rather than reported. |

Native `[JsonIgnore(Condition = JsonIgnoreCondition.WhenReading)]` conflicts with an explicit
`JsonIgnoreEx` and throws a `NotSupportedException` naming both rules, even with
`UnsupportedMemberBehavior.Skip`. Native `Always` wins; the extended attribute is ineffective. The
bundled analyzer reports these combinations. Applying `JsonIgnoreEx` twice is a compiler error.

## Constructor parameters

A constructor-bound member is reached by intercepting the property's converter, because the constructor
path never calls a setter. Consequences:

- **Only converter-based ("single-token") types are supported** — primitives, enums, `Guid`, `DateTime`,
  `string` and friends, or a type with its own member-level converter. For an object or collection
  parameter the interception would have to forward the read through `JsonConverter<T>.Read`, which spins
  up a fresh internal read stack and would silently drop reference handling, polymorphic metadata and the
  ability to resume a partially buffered read. The library refuses instead of being quietly wrong. That
  includes `object`: it is serialized polymorphically by runtime type, which a direct converter call
  bypasses, so a boxed `2` would be written as `{}`.
- **Non-default `JsonNumberHandling` on a numeric parameter is refused**: the number-handling logic lives
  in the serializer's internal converters, and a member-level converter override does not inherit it.
- A parameter with no default falls back to `default(T)`, since there is no other value to substitute.

Settable properties have none of these restrictions. Their non-nullable value types are handled by
re-declaring the member as `T?` and reusing the serializer's own `Nullable<T>` converter, which keeps
full fidelity — number handling, structs with members of their own, and resumable reads all continue to
work.

## Positional record parameters need `[property:]`

`[JsonIgnoreEx]` targets properties and fields, so on a positional record parameter write
`[property: JsonIgnoreEx(...)]`. Without the target the compiler rejects it, rather than the attribute
landing on the parameter where the contract would never see it.

## Overriding a property drops its attribute

`[JsonIgnoreEx]` on a `virtual` property does not carry over to an `override` that does not repeat it.
Attribute lookup on a property does not walk the inheritance chain; native `[JsonIgnore]` and
`[JsonPropertyName]` behave the same way. A property that is inherited without being overridden keeps
its attribute.

## `WhenReadingDefault` uses the declared type's default

The comparison is against `default` of the member's own type. For `int` that is `0`; for `int?` it is
`null`, so an incoming `0` is assigned. Declare the member as `int` if `0` should also be skipped.

## Explicit nulls are skipped, not rejected

`WhenReadingNull` on a non-nullable value type turns a JSON `null` from an error into a silent skip. If
you want the error, that is the built-in behaviour — leave the member unannotated.

## NativeAOT and trimming

The source-generated path is AOT- and trim-clean. The xUnit test project publishes with
`-p:NativeAotTests=true`, enabling `PublishAot`/`PublishTrimmed`, `TrimmerSingleWarn=false` and
`JsonSerializerIsReflectionEnabledByDefault=false`. Trim and AOT warnings are errors. The published
binary runs the shared behavioral tests against both generated resolver modes and verifies that
dynamic code and serializer reflection are disabled. Reflection-only tests run in the managed build.

Two things to know:

- **A custom enum or struct must be registered** — `ExtendedIgnoreConditionResolver.RegisterValueType<T>()`
  — because projecting the member as `T?` needs a generic instantiation over the member type, and a value
  type does not share one. The library roots the common primitive value types itself (all the integer and
  floating-point types, `bool`, `char`, `decimal`, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`,
  `TimeOnly`, `TimeSpan`, and each of their `Nullable<T>` forms), which costs about 44 rooted
  instantiations of binary size in exchange for needing no registration in the common cases. Other value
  types from .NET itself, such as `DayOfWeek`, `JsonElement` or `BigInteger`, are not rooted and count as
  custom here. Reference types share one canonical instantiation and never need registering.
- **The generator only sees `[JsonIgnoreEx]`.** It registers the custom value types of annotated members,
  including `[property:]` targets on positional records, from a module initializer in the assembly that
  declares them; the resolver runs that initializer before it looks a type up, because resolving a
  contract alone does not. It cannot see members reached only by `DefaultReadCondition`, members typed
  as a generic type parameter, types inaccessible from generated code (such as a `private` nested enum or
  a file-local type), or projects compiled below C# 9. Register those manually.
  `ExtendedIgnoreConditionSettings.RequireValueTypeRegistration = true` makes JIT runs refuse them as a
  native build would.
- **A missing registration is reported even for `DefaultReadCondition`.** Other members an options-level
  default cannot apply to are skipped silently, but an unregistered value type is a setup mistake that
  only exists under NativeAOT. Skipping it would let the same model skip nulls under JIT and throw on them
  after publishing, so the resolver throws instead, unless
  `UnsupportedMemberBehavior.Skip` is set.
- **The parameterless `ExtendedIgnoreConditionResolver()` constructor is not AOT-safe** — it falls back to
  `DefaultJsonTypeInfoResolver`, and is annotated `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`
  accordingly. Pass a source-generated context instead.

## The namespace shadows `System` inside a `Scarlet.*` namespace

`Scarlet.System.Text.Json.IgnoreCondition` matches the package id and the convention used by
`Scarlet.System.Text.Json.DateTimeConverter`, at one cost: inside a namespace that begins with
`Scarlet`, the name `System` resolves to `Scarlet.System` first. Fully qualifying a BCL type inline then
fails to compile:

```csharp
namespace Scarlet.Api;

// error CS0234: The type or namespace name 'JsonSerializer' does not exist in the
//               namespace 'Scarlet.System.Text.Json'
var x = System.Text.Json.JsonSerializer.Deserialize<Config>(json, options);
```

Use a `using` directive, as normal C# style does anyway, and nothing is affected:

```csharp
using System.Text.Json;      // resolved in global scope, above the namespace declaration

var x = JsonSerializer.Deserialize<Config>(json, options);
```

`global::System.Text.Json.JsonSerializer` also works if you must qualify inline.

## Out of scope

- Nulls **inside** collections and dictionaries (`{"Values":[null]}`). The conditions act on members, not
  on elements.
- Generic model types are supported, but each closed instantiation resolves its own contract, and a
  member whose type is a type parameter bound to a custom value type needs that value type registered as
  above.
- Type-level or assembly-level defaults beyond the options-level `DefaultReadCondition`.
