# Analyzer and code fixes

The NuGet package includes a C# analyzer and a separate IDE code-fix assembly under
`analyzers/dotnet/cs`. They target `netstandard2.0` and share the Roslyn 4.13 API baseline. Compiler and
IDE dependencies do not become runtime dependencies of your application.

The analyzer assembly also contains `ValueTypeRegistrationGenerator`, which emits a module initializer
registering the custom value types of `[JsonIgnoreEx]` members for NativeAOT. It is the same assembly
because generators and analyzers share their Roslyn dependencies; the code fixes stay separate because
they depend on the IDE workspace layer, which is not loaded during command-line builds. See the
NativeAOT section of [limitations.md](limitations.md) for what the generator cannot see.

`JsonIgnoreConditionEx` contains only `WhenReadingNull` and `WhenReadingDefault`. Use native
`JsonIgnoreAttribute` and `JsonSerializerOptions.DefaultIgnoreCondition` for standard ignore rules.

| Diagnostic | Default severity | Meaning | Fixes |
| --- | --- | --- | --- |
| SJI0001 | Warning | Native `WhenReading` conflicts with an explicit extended read condition. | Keep native `WhenReading` and remove `JsonIgnoreEx`, or keep the extended condition and remove native `JsonIgnore`. |
| SJI0002 | Warning | Native `Always`, including bare `[JsonIgnore]`, makes `JsonIgnoreEx` ineffective. | Remove `JsonIgnoreEx` while preserving the native ignore. |

Both fixes support Fix All. Removing an attribute preserves other attributes, record `property:`
targets, and surrounding comments. Fixing SJI0001 changes behavior according to the selected rule;
the fixer does not choose a policy automatically.

Complementary native write conditions and explicit `JsonIgnoreEx` conditions are valid and produce no
diagnostic. Applying `JsonIgnoreEx` twice is rejected by the C# compiler because `AllowMultiple` is
false. Aliased and fully qualified attributes are recognized by symbol identity; unrelated attributes
with the same short names are ignored. Generated code is not analyzed.

The runtime rejects SJI0001 combinations even when the analyzer is disabled. SJI0002 combinations
continue to ignore the member through the native serializer.

## Options-level defaults

An explicit `JsonIgnoreEx` takes precedence over `DefaultReadCondition`. Without it, native `Never`
opts out of the extended default; native `WhenReading` and `Always` keep their native behavior.
Native write conditions can coexist with the extended read default.

To configure diagnostics in `.editorconfig`, use their IDs:

```ini
[*.cs]
dotnet_diagnostic.SJI0001.severity = error
dotnet_diagnostic.SJI0002.severity = warning
```
