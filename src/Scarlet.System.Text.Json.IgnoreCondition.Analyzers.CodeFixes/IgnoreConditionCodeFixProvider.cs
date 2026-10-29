using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Scarlet.System.Text.Json.IgnoreCondition.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(IgnoreConditionCodeFixProvider)), Shared]
public sealed class IgnoreConditionCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
        IgnoreConditionAnalyzer.ConflictId, IgnoreConditionAnalyzer.IneffectiveId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics.First();
        var attribute = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<AttributeSyntax>();
        if (attribute is null)
            return;

        string removeTitle = diagnostic.Id == IgnoreConditionAnalyzer.ConflictId
            ? $"Keep {diagnostic.Properties["PreviousCondition"]}; remove {diagnostic.Properties["Condition"]}"
            : "Remove ineffective JsonIgnoreEx";
        context.RegisterCodeFix(CodeAction.Create(removeTitle,
            token => RemoveAsync(context.Document, attribute, token), "RemoveCurrentCondition"), diagnostic);

        if (diagnostic.Id == IgnoreConditionAnalyzer.ConflictId && diagnostic.AdditionalLocations.Count > 0 &&
            diagnostic.AdditionalLocations[0].SourceTree == root.SyntaxTree &&
            root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<AttributeSyntax>() is { } earlier)
        {
            context.RegisterCodeFix(CodeAction.Create(
                $"Keep {diagnostic.Properties["Condition"]}; remove {diagnostic.Properties["PreviousCondition"]}",
                token => RemoveAsync(context.Document, earlier, token), "RemovePreviousCondition"), diagnostic);
        }
    }

    private static async Task<Document> RemoveAsync(Document document, AttributeSyntax attribute, CancellationToken token)
    {
        var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
        SyntaxNode remove = attribute.Parent is AttributeListSyntax { Attributes.Count: 1 } list ? list : attribute;
        var member = attribute.FirstAncestorOrSelf<MemberDeclarationSyntax>()!;
        root = root!.TrackNodes(remove, member);
        var options = remove is AttributeListSyntax &&
                      remove.GetTrailingTrivia().All(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia))
            ? SyntaxRemoveOptions.KeepLeadingTrivia : SyntaxRemoveOptions.KeepExteriorTrivia;
        var changed = root.RemoveNode(root.GetCurrentNode(remove)!, options | SyntaxRemoveOptions.KeepUnbalancedDirectives)!;
        var currentMember = changed.GetCurrentNode(member);
        if (currentMember is not null)
            changed = changed.ReplaceNode(currentMember, currentMember.WithAdditionalAnnotations(Formatter.Annotation));
        return document.WithSyntaxRoot(changed);
    }
}
