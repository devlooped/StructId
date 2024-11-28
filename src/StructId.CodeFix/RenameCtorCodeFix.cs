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
public class RenameCtorCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(Diagnostics.MustHaveValueConstructor.Id);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var parameter = root.FindNode(context.Span).FirstAncestorOrSelf<ParameterSyntax>();
        if (parameter == null)
            return;

        context.RegisterCodeFix(
            new RenameAction(context.Document, root, parameter),
            context.Diagnostics);
    }

    public class RenameAction(Document document, SyntaxNode root, ParameterSyntax parameter) : CodeAction
    {
        public override string Title => "Rename to 'Value' as required for struct ids";
        public override string EquivalenceKey => Title;

        protected override Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(parameter,
                parameter.WithIdentifier(Identifier("Value")))));
        }
    }
}