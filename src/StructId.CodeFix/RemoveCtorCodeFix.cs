using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace StructId;

[Shared]
[ExportCodeFixProvider(LanguageNames.CSharp)]
public class RemoveCtorCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(Diagnostics.MustHaveValueConstructor.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var declaration = root.FindNode(context.Span).FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (declaration == null)
            return;

        if (declaration.ParameterList?.Parameters.Count == 1)
            context.RegisterCodeFix(
                new RemoveAction(context.Document, root, declaration),
                context.Diagnostics);
    }

    public class RemoveAction(Document document, SyntaxNode root, TypeDeclarationSyntax declaration) : CodeAction
    {
        public override string Title => "Remove primary constructor to generate it automatically";
        public override string EquivalenceKey => Title;

        protected override Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(declaration,
                declaration.WithParameterList(null))));
        }
    }
}