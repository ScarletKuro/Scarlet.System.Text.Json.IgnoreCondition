; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|--------------|----------|-------
SJIC001 | Scarlet.Json | Error | Read-side ignore condition on a required member
SJIC002 | Scarlet.Json | Error | Read-side ignore condition on a member that cannot be assigned
SJIC003 | Scarlet.Json | Error | [JsonIgnoreEx] combined with [JsonIgnore]
SJIC004 | Scarlet.Json | Error | Two ignore conditions on the same side
SJIC005 | Scarlet.Json | Warning | Ignore condition needs converter interception on a type that does not support it
SJIC006 | Scarlet.Json | Warning | Ignore condition conflicts with JsonNumberHandling
SJIC007 | Scarlet.Json | Warning | [JsonIgnoreEx] on extension data
SJIC008 | Scarlet.Json | Info | Explicit JSON null will be silently skipped
SJIC009 | Scarlet.Json | Info | Extended ignore conditions are never installed
SJIC010 | Scarlet.Json | Warning | Generic type is not registered at compile time
SJIC011 | Scarlet.Json | Warning | Type is not accessible to the generated registry
