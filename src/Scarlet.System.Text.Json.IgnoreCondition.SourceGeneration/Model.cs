using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Scarlet.Text.Json.SourceGeneration;

/// <summary>A diagnostic captured during analysis, in a form that survives incremental caching.</summary>
internal sealed class DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    public DiagnosticInfo(string id, Location? location, params string[] messageArgs)
    {
        Id = id;
        MessageArgs = messageArgs;
        FilePath = location?.SourceTree?.FilePath ?? string.Empty;
        Span = location?.SourceSpan ?? default;
        LineSpan = location?.GetLineSpan().Span ?? default;
    }

    public string Id { get; }

    public string[] MessageArgs { get; }

    public string FilePath { get; }

    public TextSpan Span { get; }

    public LinePositionSpan LineSpan { get; }

    public Diagnostic? ToDiagnostic()
    {
        DiagnosticDescriptor? descriptor = Diagnostics.ById(Id);
        if (descriptor is null)
        {
            return null;
        }

        Location location = FilePath.Length == 0
            ? Location.None
            : Location.Create(FilePath, Span, LineSpan);

        return Diagnostic.Create(descriptor, location, MessageArgs);
    }

    public bool Equals(DiagnosticInfo? other) =>
        other is not null &&
        Id == other.Id &&
        FilePath == other.FilePath &&
        Span == other.Span &&
        MessageArgs.SequenceEqual(other.MessageArgs);

    public override bool Equals(object? obj) => Equals(obj as DiagnosticInfo);

    public override int GetHashCode()
    {
        int hash = Id.GetHashCode();
        hash = (hash * 397) ^ FilePath.GetHashCode();
        hash = (hash * 397) ^ Span.GetHashCode();
        foreach (string arg in MessageArgs)
        {
            hash = (hash * 397) ^ arg.GetHashCode();
        }

        return hash;
    }
}

/// <summary>One member's registration, already rendered as the builder call that declares it.</summary>
internal sealed class MemberRegistration : IEquatable<MemberRegistration>
{
    public MemberRegistration(string memberName, string builderCall)
    {
        MemberName = memberName;
        BuilderCall = builderCall;
    }

    public string MemberName { get; }

    public string BuilderCall { get; }

    public bool Equals(MemberRegistration? other) =>
        other is not null && MemberName == other.MemberName && BuilderCall == other.BuilderCall;

    public override bool Equals(object? obj) => Equals(obj as MemberRegistration);

    public override int GetHashCode() => (MemberName.GetHashCode() * 397) ^ BuilderCall.GetHashCode();
}

/// <summary>Everything the emitter needs about one annotated type, plus its diagnostics.</summary>
internal sealed class TypeRegistration : IEquatable<TypeRegistration>
{
    public TypeRegistration(
        string typeExpression,
        string safeName,
        IReadOnlyList<MemberRegistration> members,
        IReadOnlyList<DiagnosticInfo> diagnostics)
    {
        TypeExpression = typeExpression;
        SafeName = safeName;
        Members = members;
        Diagnostics = diagnostics;
    }

    /// <summary>The <c>global::</c>-qualified type name used inside <c>typeof(...)</c>.</summary>
    public string TypeExpression { get; }

    /// <summary>An identifier-safe form of the type name, used to name the generated method.</summary>
    public string SafeName { get; }

    public IReadOnlyList<MemberRegistration> Members { get; }

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }

    public bool Equals(TypeRegistration? other) =>
        other is not null &&
        TypeExpression == other.TypeExpression &&
        SafeName == other.SafeName &&
        Members.SequenceEqual(other.Members) &&
        Diagnostics.SequenceEqual(other.Diagnostics);

    public override bool Equals(object? obj) => Equals(obj as TypeRegistration);

    public override int GetHashCode()
    {
        int hash = TypeExpression.GetHashCode();
        foreach (MemberRegistration member in Members)
        {
            hash = (hash * 397) ^ member.GetHashCode();
        }

        foreach (DiagnosticInfo diagnostic in Diagnostics)
        {
            hash = (hash * 397) ^ diagnostic.GetHashCode();
        }

        return hash;
    }
}
