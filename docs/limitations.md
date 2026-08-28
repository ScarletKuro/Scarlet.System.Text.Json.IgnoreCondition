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
| Also carries `[JsonIgnore]` | `[JsonIgnore]` wins. The serializer keeps the contract entry but strips both accessors, so the extended condition has nothing to act on and is skipped rather than reported. |

Two conditions on the same side (two read-side, or two write-side) also throws.

## Constructor parameters

A constructor-bound member is reached by intercepting the property's converter, because the constructor
path never calls a setter. Consequences:

- **Only converter-based ("single-token") types are supported** — primitives, enums, `Guid`, `DateTime`,
  `string` and friends, or a type with its own member-level converter. For an object or collection
  parameter the interception would have to forward the read through `JsonConverter<T>.Read`, which spins
  up a fresh internal read stack and would silently drop reference handling, polymorphic metadata and the
  ability to resume a partially buffered read. The library refuses instead of being quietly wrong.
- **Non-default `JsonNumberHandling` on a numeric parameter is refused**: the number-handling logic lives
  in the serializer's internal converters, and a member-level converter override does not inherit it.
- `Always` and `WhenReading` are refused outright: the constructor argument has to be supplied.
- A parameter with no default falls back to `default(T)`, since there is no other value to substitute.

Settable properties have none of these restrictions. Their non-nullable value types are handled by
re-declaring the member as `T?` and reusing the serializer's own `Nullable<T>` converter, which keeps
full fidelity — number handling, structs with members of their own, and resumable reads all continue to
work.

## Positional record parameters need `[property:]`

`[JsonIgnoreEx]` targets properties and fields, so on a positional record parameter write
`[property: JsonIgnoreEx(...)]`. Without the target the compiler rejects it, rather than the attribute
landing on the parameter where the contract would never see it.

## Explicit nulls are skipped, not rejected

`WhenReadingNull` on a non-nullable value type turns a JSON `null` from an error into a silent skip. If
you want the error, that is the built-in behaviour — leave the member unannotated.

## NativeAOT and trimming

The source-generated path is AOT- and trim-clean. `tests/…AotSample` publishes with
`PublishAot`/`PublishTrimmed`, `TrimmerSingleWarn=false` and
`JsonSerializerIsReflectionEnabledByDefault=false` to keep it that way, and asserts the behaviour at run
time.

Two things to know:

- **A custom enum or struct must be registered** — `ExtendedIgnoreConditionResolver.RegisterValueType<T>()`
  — because projecting the member as `T?` needs a generic instantiation over the member type, and a value
  type does not share one. The library roots every built-in value type itself (all the integer and
  floating-point types, `bool`, `char`, `decimal`, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`,
  `TimeOnly`, `TimeSpan`, and each of their `Nullable<T>` forms), which costs about 44 rooted
  instantiations of binary size in exchange for needing no registration in the common cases. Reference
  types share one canonical instantiation and never need registering.
- **The parameterless `ExtendedIgnoreConditionResolver()` constructor is not AOT-safe** — it falls back to
  `DefaultJsonTypeInfoResolver`, and is annotated `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`
  accordingly. Pass a source-generated context instead.

## Out of scope

- Nulls **inside** collections and dictionaries (`{"Values":[null]}`). The conditions act on members, not
  on elements.
- Generic model types are supported, but each closed instantiation resolves its own contract, and a
  member whose type is a type parameter bound to a custom value type needs that value type registered as
  above.
- Type-level or assembly-level defaults beyond the options-level `DefaultReadCondition` /
  `DefaultWriteCondition`.
