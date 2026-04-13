using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static StructId.Diagnostics;

namespace StructId;

[Shared]
[ExportCodeFixProvider(LanguageNames.CSharp)]
public class TemplateCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
        TemplateMustBeFileRecordStruct.Id, TemplateDeclarationNotTSelf.Id);

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
        public override string Title => "Change to file-local partial record struct TSelf";
        public override string EquivalenceKey => Title;

        protected override Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
        {
            var declaration = original;
            var modifiers = declaration.Modifiers;

            if (!modifiers.Any(SyntaxKind.FileKeyword))
            {
                var file = SpacedToken(SyntaxKind.FileKeyword);
                if (modifiers.Count > 0)
                {
                    var firstModifier = modifiers[0];
                    file = file.WithLeadingTrivia(firstModifier.LeadingTrivia);
                    modifiers = modifiers.Replace(firstModifier, firstModifier.WithLeadingTrivia(TriviaList()));
                }

                modifiers = modifiers.Insert(0, file);
            }

            if (!modifiers.Any(SyntaxKind.PartialKeyword))
                modifiers = modifiers.Insert(1, SpacedToken(SyntaxKind.PartialKeyword));

            // Remove accessibility modifiers which are replaced by 'file' visibility
            if (modifiers.FirstOrDefault(x => x.IsKind(SyntaxKind.PublicKeyword)) is { } @public)
                modifiers = modifiers.Remove(@public);
            if (modifiers.FirstOrDefault(x => x.IsKind(SyntaxKind.InternalKeyword)) is { } @internal)
                modifiers = modifiers.Remove(@internal);
            if (modifiers.FirstOrDefault(x => x.IsKind(SyntaxKind.PrivateKeyword)) is { } @private)
                modifiers = modifiers.Remove(@private);

            if (declaration.Identifier.Text != "TSelf")
                declaration = declaration.WithIdentifier(Identifier(
                    declaration.Identifier.LeadingTrivia,
                    "TSelf",
                    declaration.Identifier.TrailingTrivia));

            if (!declaration.IsKind(SyntaxKind.RecordStructDeclaration))
            {
                declaration = RecordDeclaration(
                    SyntaxKind.RecordStructDeclaration,
                    declaration.AttributeLists,
                    modifiers,
                    SpacedToken(SyntaxKind.RecordKeyword),
                    SpacedToken(SyntaxKind.StructKeyword),
                    declaration.Identifier,
                    declaration.TypeParameterList,
                    declaration.ParameterList,
                    declaration.BaseList,
                    declaration.ConstraintClauses,
                    declaration.OpenBraceToken,
                    declaration.Members,
                    declaration.CloseBraceToken,
                    declaration.SemicolonToken)
                    .WithTriviaFrom(original);
            }
            else if (modifiers != declaration.Modifiers)
            {
                declaration = declaration.WithModifiers(modifiers);
            }

            return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(original, declaration)));
        }

        static SyntaxToken SpacedToken(SyntaxKind kind)
            => Token(TriviaList(), kind, TriviaList(Space));
    }
}
