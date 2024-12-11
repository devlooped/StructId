using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace StructId;

public static class CodeTemplate
{
    public static SyntaxNode Parse(string template, CSharpParseOptions? parseOptions = default)
    {
        var tree = CSharpSyntaxTree.ParseText(template,
            parseOptions ?? CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

        return tree.GetRoot();
    }

    public static string Apply(string template, string structIdType, string valueType, bool normalizeWhitespace = false)
    {
        var targetNamespace = structIdType.Contains('.') ? structIdType.Substring(0, structIdType.LastIndexOf('.')) : null;
        structIdType = structIdType.Contains('.') ? structIdType.Substring(structIdType.LastIndexOf('.') + 1) : structIdType;

        var applied = ApplyImpl(Parse(template), structIdType, valueType, targetNamespace);

        return normalizeWhitespace ?
            applied.NormalizeWhitespace().ToFullString() :
            applied.ToFullString();
    }

    public static string Apply(string template, string valueType, bool normalizeWhitespace = false)
    {
        var applied = ApplyImpl(Parse(template), valueType);

        return normalizeWhitespace ?
            applied.NormalizeWhitespace().ToFullString().Trim() :
            applied.ToFullString().Trim();
    }

    public static SyntaxNode ApplyValue(this SyntaxNode node, INamedTypeSymbol valueType) => ApplyImpl(node, valueType.ToFullName());

    public static SyntaxNode Apply(this SyntaxNode node, INamedTypeSymbol structId)
    {
        var root = node.SyntaxTree.GetCompilationUnitRoot();
        if (root == null)
            return node;

        // determine namespace of the IStructId/IStructId<T> interface implemented by structId
        var iface = structId.Interfaces.FirstOrDefault(x => x.Name == "IStructId");
        if (iface == null)
            return root;

        var tid = iface.TypeArguments.FirstOrDefault()?.ToFullName() ?? "string";
        var corens = iface.ContainingNamespace.ToFullName();
        var targetNamespace = structId.ContainingNamespace != null && !structId.ContainingNamespace.IsGlobalNamespace ?
            structId.ContainingNamespace.ToDisplayString() : null;

        return ApplyImpl(root, structId.Name, tid, targetNamespace, corens);
    }

    static SyntaxNode ApplyImpl(this SyntaxNode node, string valueType)
    {
        var root = node.SyntaxTree.GetCompilationUnitRoot();
        if (root == null)
            return node;

        node = new ValueRewriter(valueType).Visit(root)!;

        return node;
    }

    static SyntaxNode ApplyImpl(this SyntaxNode node, string structIdType, string valueType, string? targetNamespace = default, string coreNamespace = "StructId")
    {
        var root = node.SyntaxTree.GetCompilationUnitRoot();
        if (root == null)
            return node;

        // If we got a ns, move all members after a file-scoped namespace declaration
        if (targetNamespace != null)
        {
            var members = root.Members;
            var fsns = FileScopedNamespaceDeclaration(ParseName(targetNamespace)
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithLeadingTrivia(Whitespace(" ")))
                .WithLeadingTrivia(LineFeed)
                .WithTrailingTrivia(LineFeed, LineFeed)
                .WithMembers(members);

            root = root.WithMembers(SingletonList<MemberDeclarationSyntax>(fsns));
        }

        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
        // There should be NO namespace declared in the template itself, since we enforce file-local
        usings.Add(UsingDirective(ParseName(coreNamespace)).NormalizeWhitespace());

        // deduplicate usings just in case
        var unique = new HashSet<string>();
        root = root.ReplaceNodes(usings, (old, _) =>
        {
            // replace 'StructId' > StructIdNamespace
            if (old.Name?.ToString() == "StructId")
            {
                unique.Add(coreNamespace);
                return old.WithName(ParseName(coreNamespace));
            }

            if (unique.Add(old.Name?.ToString() ?? ""))
                return old;

            return null!;
        });

        node = new TemplateRewriter(structIdType, valueType).Visit(root)!;

        return node;
    }

    class ValueRewriter(string tvalue) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            if (IsFileLocal(node))
                return null;

            return node;
        }

        public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
        {
            if (IsFileLocal(node))
                return null;

            return base.VisitStructDeclaration(node);
        }

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (IsFileLocal(node))
                return null;

            return base.VisitClassDeclaration(node);
        }

        public override SyntaxNode? VisitAttribute(AttributeSyntax node)
        {
            if (node.IsValueTemplate())
                return null;

            return base.VisitAttribute(node);
        }

        public override SyntaxNode? VisitAttributeList(AttributeListSyntax node)
        {
            node = (AttributeListSyntax)base.VisitAttributeList(node)!;
            if (node.Attributes.Count == 0)
                return null;

            return base.VisitAttributeList(node);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.Text == "TValue")
                return IdentifierName(tvalue)
                    .WithLeadingTrivia(node.Identifier.LeadingTrivia)
                    .WithTrailingTrivia(node.Identifier.TrailingTrivia);

            if (node.Identifier.Text.StartsWith("TValue_"))
                return IdentifierName(node.Identifier.Text.Replace("TValue_", tvalue.Replace('.', '_') + "_"))
                    .WithLeadingTrivia(node.Identifier.LeadingTrivia)
                    .WithTrailingTrivia(node.Identifier.TrailingTrivia);

            return base.VisitIdentifierName(node);
        }

        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            if (token.IsKind(SyntaxKind.IdentifierToken) && token.Text == "TValue")
                return Identifier(tvalue)
                    .WithLeadingTrivia(token.LeadingTrivia)
                    .WithTrailingTrivia(token.TrailingTrivia);

            if (token.IsKind(SyntaxKind.IdentifierToken) && token.Text.StartsWith("TValue_"))
                return Identifier(token.Text.Replace("TValue_", tvalue.Replace('.', '_') + "_"))
                    .WithLeadingTrivia(token.LeadingTrivia)
                    .WithTrailingTrivia(token.TrailingTrivia);

            return base.VisitToken(token);
        }

        bool IsFileLocal(TypeDeclarationSyntax node) =>
            node.Modifiers.Any(x => x.IsKind(SyntaxKind.FileKeyword)) &&
            !node.AttributeLists.Any(list => list.Attributes.Any(a => a.IsValueTemplate()));
    }

    class TemplateRewriter(string tself, string tid) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            // remove file-local records that aren't annotated with [TStructId]
            if (node.Modifiers.Any(x => x.IsKind(SyntaxKind.FileKeyword)) &&
                !node.AttributeLists.Any(list => list.Attributes.Any(a => a.IsStructIdTemplate())))
                return null;

            // If the record has the [TStructId] attribute, remove primary ctor
            if (node.AttributeLists.Any(list => list.Attributes.Any(a => a.IsStructIdTemplate())) &&
                node.ParameterList is { } parameters)
            {
                // Check if the open paren trivia contains the text '🙏' and remove it
                // This is used to signal that the primary ctor should not be removed. 
                // This is the case with the ctor templates.
                if (parameters.OpenParenToken.GetAllTrivia().Any(x => x.ToString().Contains("🙏")))
                    node = node.WithParameterList(parameters
                        .WithOpenParenToken(parameters.OpenParenToken.WithoutTrivia()));
                else
                    node = node.WithParameterList(null);

                node = node.WithIdentifier(node.Identifier.WithTrailingTrivia(parameters.CloseParenToken.TrailingTrivia));
            }

            var visited = (RecordDeclarationSyntax)base.VisitRecordDeclaration(node)!;
            var trivia = TriviaList();
            // Rather than removing the empty attribute lists via the VisitAttributeList method, we do it here
            // so we can preserve the trivia.
            foreach (var list in visited.AttributeLists)
            {
                if (list.Attributes.Count == 0)
                {
                    trivia = trivia.AddRange(list.GetLeadingTrivia());
                    visited = visited.RemoveNode(list, SyntaxRemoveOptions.KeepNoTrivia)!;
                }
            }

            if (trivia.Count > 0)
                visited = visited.WithLeadingTrivia(trivia);

            // remove file modifier from type declarations
            if (visited.Modifiers.FirstOrDefault(x => x.IsKind(SyntaxKind.FileKeyword)) is { } file)
            {
                // Preserve trivia, i.e. newline from original file modifier, as well as potentially 
                // other trivia we might have added from removed attribute lists
                visited = visited
                    .WithModifiers(visited.Modifiers.Remove(file))
                    .WithLeadingTrivia(file.LeadingTrivia);
            }

            return visited;
        }

        public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
        {
            // remove file-local structs that aren't annotated with [TStructId]
            if (node.Modifiers.Any(x => x.IsKind(SyntaxKind.FileKeyword)) &&
                !node.AttributeLists.Any(list => list.Attributes.Any(a => a.IsStructIdTemplate())))
                return null;

            return base.VisitStructDeclaration(node);
        }

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            // remove file-local classes (they can't be annotated with [TStructId])
            if (node.Modifiers.Any(x => x.IsKind(SyntaxKind.FileKeyword)))
                return null;

            return base.VisitClassDeclaration(node);
        }

        public override SyntaxNode? VisitAttribute(AttributeSyntax node)
        {
            if (node.IsStructIdTemplate())
                return null;

            return base.VisitAttribute(node);
        }

        // rewrite references to the original type with the target type
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.Text == "TSelf")
                return IdentifierName(tself)
                    .WithLeadingTrivia(node.Identifier.LeadingTrivia)
                    .WithTrailingTrivia(node.Identifier.TrailingTrivia);
            else if (node.Identifier.Text == "TId" || node.Identifier.Text == "TValue")
                return IdentifierName(tid)
                    .WithLeadingTrivia(node.Identifier.LeadingTrivia)
                    .WithTrailingTrivia(node.Identifier.TrailingTrivia);

            return base.VisitIdentifierName(node);
        }

        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            // if token is an identifier token, rewrite it
            if (token.IsKind(SyntaxKind.IdentifierToken) && token.Text == "TSelf")
                return Identifier(tself)
                    .WithLeadingTrivia(token.LeadingTrivia)
                    .WithTrailingTrivia(token.TrailingTrivia);
            else if (token.IsKind(SyntaxKind.IdentifierToken) && (token.Text == "TId" || token.Text == "TValue"))
                return Identifier(tid)
                    .WithLeadingTrivia(token.LeadingTrivia)
                    .WithTrailingTrivia(token.TrailingTrivia);

            return base.VisitToken(token);
        }
    }
}
