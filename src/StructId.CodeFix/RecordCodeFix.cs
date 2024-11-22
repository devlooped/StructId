using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace StructId;

[Shared]
[ExportCodeFixProvider(LanguageNames.CSharp)]
public class RecordCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(Diagnostics.MustBeRecordStruct.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var declaration = root.FindNode(context.Span).FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (declaration == null)
            return;

        context.RegisterCodeFix(
            new FixerAction(context.Document, root, declaration),
            context.Diagnostics);
    }

    public class FixerAction(Document document, SyntaxNode root, TypeDeclarationSyntax original) : CodeAction
    {
        public override string Title => "Change to readonly partial record struct";
        public override string EquivalenceKey => Title;

        protected override Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
        {
            var declaration = original;

            if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                declaration = declaration.AddModifiers(Token(SyntaxKind.PartialKeyword));

            if (!declaration.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
            {
                var visibility = declaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.PublicKeyword) || m.IsKind(SyntaxKind.InternalKeyword));
                var index = visibility == null ? 0 : declaration.Modifiers.IndexOf(visibility) + 1;
                declaration = declaration.WithModifiers(declaration.Modifiers.Insert(index, Token(SyntaxKind.ReadOnlyKeyword)));
            }

            if (!declaration.IsKind(SyntaxKind.RecordStructDeclaration))
            {
                declaration = RecordDeclaration(
                    SyntaxKind.RecordStructDeclaration,
                    declaration.AttributeLists,
                    declaration.Modifiers,
                    Token(SyntaxKind.RecordKeyword),
                    Token(SyntaxKind.StructKeyword),
                    declaration.Identifier,
                    declaration.TypeParameterList,
                    declaration.ParameterList,
                    declaration.BaseList,
                    declaration.ConstraintClauses,
                    declaration.OpenBraceToken,
                    declaration.Members,
                    declaration.CloseBraceToken,
                    declaration.SemicolonToken);
            }

            return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(original, declaration)));                    
        }
    }
}