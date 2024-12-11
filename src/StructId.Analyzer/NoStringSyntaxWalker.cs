using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StructId;

class NoStringSyntaxWalker : CSharpSyntaxWalker
{
    bool nostring;

    public bool Accept(SyntaxNode node)
    {
        Visit(node);
        return nostring;
    }

    // visit primary constructor and check if there's a trivia with "/*!string*/"
    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        if (node.AttributeLists.Any(list => list.Attributes.Any(a => a.IsStructIdTemplate())) &&
            node.ParameterList is { } parameters &&
            parameters.OpenParenToken.GetAllTrivia().Any(x => x.ToString().Contains("!string")))
        {
            nostring = true;
        }
    }
}
