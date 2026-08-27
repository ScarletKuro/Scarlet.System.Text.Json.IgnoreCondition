# Limitations

Everything here is a deliberate boundary, and each one is either an analyzer diagnostic at compile time
or a `NotSupportedException` naming the member when the contract is resolved. Set
`ExtendedIgnoreConditionSettings.UnsupportedMemberBehavior = UnsupportedMemberBehavior.Skip` to leave
the built-in behaviour in place instead of throwing.

## The source-generated fast path is disabled for annotated types

`JsonTypeInfo.DetermineIsCompatibleWithCurrentOptions` returns false as soon as a contract has been
customized, which switches off the serialization fast path the `System.Text.Json` generator emits.
Measured at roughly 1.4× on serialization; see the table in the README.

This is unavoidable for any implementation of this feature — the whole point is to customize the
contract. What the library does control is the blast radius: a type with no `[JsonIgnoreEx]` and no
applicable options-level default is not touched at all, so it keeps its fast path.

## Read-side conditions cannot apply to some members

| Member | Why | Rule |
| --- | --- | --- |
| `required` | The serializer must observe a value for it, so there is nothing to skip. | SJIC001 |
| No setter and not constructor-bound | There is no assignment to skip. | SJIC002 |
| `[JsonExtensionData]` | Extension data is not a normal member. | SJIC007 |
| Also carries `[JsonIgnore]` | `[JsonIgnore]` removes the member from the contract first. | SJIC003 |

## Constructor parameters

A constructor-bound member is reached by intercepting the property's converter, because the
constructor path never calls a setter. Two consequences:

- **Only converter-based ("single-token") types are supported** — primitives, enums, `Guid`,
  `DateTime`, `string` and friends, or a type with its own member-level converter. For an object or
  collection parameter the interception would have to forward the read through
  `JsonConverter<T>.Read`, which spins up a fresh internal read stack and would silently drop
  reference handling, polymorphic metadata and the ability to resume a partially buffered read. The
  library refuses instead of being quietly wrong (SJIC005).
- **Non-default `JsonNumberHandling` on a numeric parameter is refused** (SJIC006): the number-handling
  logic lives in the serializer's internal converters, and a member-level converter override does not
  inherit it.
- `Always` and `WhenReading` are refused outright: the constructor argument has to be supplied.
- A parameter with no default falls back to `default(T)`, since there is no other value to substitute.

Settable properties do not have any of these restrictions. Their non-nullable value types are handled
by re-declaring the member as `T?` and reusing the serializer's own `Nullable<T>` converter, which
keeps full fidelity — number handling, complex structs, and resumable reads all continue to work.

## Explicit nulls are skipped, not rejected

`WhenReadingNull` on a non-nullable value type turns a JSON `null` from an error into a silent skip
(SJIC008, informational). If you want the error, that is the built-in behaviour — leave the member
unannotated.

## NativeAOT and trimming

The source-generated path is AOT- and trim-clean; the AotSample project publishes with
`PublishAot`/`PublishTrimmed`, `TrimmerSingleWarn=false` and
`JsonSerializerIsReflectionEnabledByDefault=false` to keep it that way.

Two paths are not AOT-safe, and both are annotated so the analyzers say so at the call site:

- **`AddExtendedIgnoreConditions()`** falls back to `DefaultJsonTypeInfoResolver` when no resolver has
  been configured. Use `WithExtendedIgnoreConditions()` on a generated context instead.
- **The reflection fallback**, used for assemblies the generator did not process, needs a generic
  instantiation per member type. Set `AllowReflectionFallback = false` to make a missing registration
  fail loudly rather than work on one platform only.

The same applies to an **options-level default** (`DefaultReadCondition`) landing on a non-nullable
value-type member or a constructor parameter in an assembly the generator never saw: producing that
member's typed metadata needs runtime code generation. Annotate the member explicitly with
`[JsonIgnoreEx]` so the generator emits it at compile time. On a platform without dynamic code the
default is skipped for that member rather than misapplied.

## Not registered at compile time

- **Generic types** (SJIC010) — the registry is keyed on a closed type, and member types may involve
  type parameters. Handled by the reflection fallback.
- **Types not accessible from assembly scope** (SJIC011), e.g. a `private` nested class — the generated
  registry could not name them in a `typeof(...)`. Handled by the reflection fallback.

## Out of scope

- Nulls **inside** collections and dictionaries (`{"Values":[null]}`). The conditions act on members,
  not on elements.
- Type-level or assembly-level defaults beyond the options-level `DefaultReadCondition` /
  `DefaultWriteCondition`.
- Registering a type after the serializer has already resolved its contract: `System.Text.Json` caches
  contracts per options instance. The generator's module initializer runs before anything in the
  assembly can be used, so this only affects hand-written `ExtendedIgnoreConditionRegistry.Register`
  calls.
