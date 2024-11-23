using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class ParsableGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Locate the IParseable<T> type
        var parseable = context.CompilationProvider
            .Select((x, _) => x.GetTypeByMetadataName("System.IParsable`1"));

        var ids = context.CompilationProvider
            .SelectMany((x, _) =>  x.Assembly.GetAllTypes().OfType<INamedTypeSymbol>())
            .Where(x => x.IsStructId())
            .Where(x => x.IsPartial());

        var combined = ids.Combine(parseable)
            .Where(x =>
            {
                var (id, parseable) = x;

                // NOTE: we never generate for compilations that don't have the IParsable<T> type (i.e. .NET6)
                if (parseable == null)
                    return false;

                var type = id.AllInterfaces
                    .First(x => x.Name == "IStructId")
                    .TypeArguments.FirstOrDefault();

                // If we don't have a generic type of IStructId, then it's the string-based one
                // which we can always parse
                if (type == null)
                    return true;

                return type.Is(parseable);
            })
            .Select((x, _) => x.Left);

        context.RegisterImplementationSourceOutput(combined, GenerateCode);
    }

    void GenerateCode(SourceProductionContext context, INamedTypeSymbol symbol)
    {
        var ns = symbol.ContainingNamespace.Equals(symbol.ContainingModule.GlobalNamespace, SymbolEqualityComparer.Default)
            ? null
            : symbol.ContainingNamespace.ToDisplayString();

        // Generic IStructId<T> -> T, otherwise string
        var type = symbol.AllInterfaces.First(x => x.Name == "IStructId").TypeArguments.
            Select(x => x.GetTypeName(ns)).FirstOrDefault() ?? "string";

        var template = type == "string" 
            ? ThisAssembly.Resources.Templates.SParseable.Text 
            : ThisAssembly.Resources.Templates.TParseable.Text;

        // parse template into a C# compilation unit
        var parseable = CSharpSyntaxTree.ParseText(template).GetCompilationUnitRoot();

        // if we got a ns, move all members after a file-scoped namespace declaration
        if (ns != null)
        {
            var members = parseable.Members;
            var fsns = FileScopedNamespaceDeclaration(ParseName(ns).WithLeadingTrivia(Whitespace(" ")))
                .WithLeadingTrivia(LineFeed)
                .WithTrailingTrivia(LineFeed)
                .WithMembers(members);
            parseable = parseable.WithMembers(SingletonList<MemberDeclarationSyntax>(fsns));
        }

        // replace all nodes with the identifier TStruct/SStruct with symbol.Name
        var structIds = parseable.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(x => x.Identifier.Text == "TStruct" || x.Identifier.Text == "SStruct");
        parseable = parseable.ReplaceNodes(structIds, (node, _) => IdentifierName(symbol.Name)
            .WithLeadingTrivia(node.GetLeadingTrivia())
            .WithTrailingTrivia(node.GetTrailingTrivia()));

        var structTokens = parseable.DescendantTokens()
            .OfType<SyntaxToken>()
            .Where(x => x.IsKind(SyntaxKind.IdentifierToken))
            .Where(x => x.Text == "TStruct" || x.Text == "SStruct");
        // replace with a new identifier with symbol.name
        parseable = parseable.ReplaceTokens(structTokens, (token, _) => Identifier(symbol.Name)
            .WithLeadingTrivia(token.LeadingTrivia)
            .WithTrailingTrivia(token.TrailingTrivia));

        // replace all nodes with the identifier TValue with actual type
        var placeholder = parseable.DescendantNodes().OfType<IdentifierNameSyntax>().Where(x => x.Identifier.Text == "TValue");
        parseable = parseable.ReplaceNodes(placeholder, (_, _) => IdentifierName(type));
        
        context.AddSource($"{symbol.ToFileName()}.parsable.cs", SourceText.From(parseable.ToFullString(), Encoding.UTF8));
    }
}
