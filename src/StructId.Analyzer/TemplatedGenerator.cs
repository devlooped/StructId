using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class TemplatedGenerator : IIncrementalGenerator
{
    record KnownTypes(string StructIdNamespace, INamedTypeSymbol String, INamedTypeSymbol? IStructId, INamedTypeSymbol? IStructIdT, INamedTypeSymbol? TStructId, INamedTypeSymbol? TStructIdT);
    record IdTemplate(INamedTypeSymbol StructId, Template Template);
    record Template(INamedTypeSymbol TSelf, ITypeSymbol TId, AttributeData Attribute, string StructIdNamespace, bool IsGenericTId)
    {
        public Regex NameExpr { get; } = new Regex($@"\b{TSelf.Name}\b", RegexOptions.Compiled | RegexOptions.Multiline);

        public string Text { get; } = GetTemplateCode(TSelf, TId, Attribute, StructIdNamespace);

        static string GetTemplateCode(INamedTypeSymbol self, ITypeSymbol tid, AttributeData attribute, string StructIdNamespace)
        {
            if (self.DeclaringSyntaxReferences[0].GetSyntax() is not TypeDeclarationSyntax declaration)
                return "";

            // Remove the TId/TValue if present in the same syntax tree.
            var toremove = tid.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).ToList();
            // Also the [TStructId<T>] attribute applied to the template itself
            if (attribute.ApplicationSyntaxReference?.GetSyntax().FirstAncestorOrSelf<AttributeListSyntax>() is { } attr)
                toremove.Add(attr);
            // And the primary constructor if present, since that's generated for the struct id already
            if (declaration.ParameterList != null)
                toremove.Add(declaration.ParameterList);

            var root = declaration.SyntaxTree.GetRoot()
                .RemoveNodes(toremove, SyntaxRemoveOptions.KeepLeadingTrivia)!;

            var update = root.DescendantNodes().OfType<TypeDeclarationSyntax>().First(x => x.Identifier.Text == self.Name);

            // Remove file-scoped modifier if present
            if (update.Modifiers.FirstOrDefault(x => x.IsKind(SyntaxKind.FileKeyword)) is { } file)
            {
                var updated = update.WithModifiers(update.Modifiers.Remove(file));
                // Preserve trivia, i.e. newline from original file modifier
                if (updated.Modifiers.Count > 0)
                    updated = updated.ReplaceToken(updated.Modifiers[0], updated.Modifiers[0].WithLeadingTrivia(file.LeadingTrivia));

                root = root.ReplaceNode(update, updated);
            }

            // replace usings/namespace from StructId > StructIdNamespace
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
            var ns = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            var nsname = ns?.Name.ToString();

            if (nsname == "StructId")
                root = root.ReplaceNode(ns!, ns!.WithName(ParseName(StructIdNamespace)));
            else if (nsname != StructIdNamespace)
                usings.Add(UsingDirective(ParseName(StructIdNamespace)).NormalizeWhitespace());

            // deduplicate usings just in case
            var unique = new HashSet<string>();
            root = root.ReplaceNodes(usings, (old, _) =>
            {
                // replace 'StructId' > StructIdNamespace
                if (old.Name?.ToString() == "StructId")
                {
                    unique.Add(StructIdNamespace);
                    return old.WithName(ParseName(StructIdNamespace));
                }

                if (unique.Add(old.Name?.ToString() ?? ""))
                    return old;

                return null!;
            });

            // rewrite Value references to explicit casts just in case the 
            // target type is implemented explicitly.
            root = new ValueRewriter(tid).Visit(root);

            var code = root.SyntaxTree.GetRoot().ToFullString().Trim();

            return code;
        }
    }

    // create a syntax rewriter that replaces references to the Value property with an explicit 
    // cast of that property to a given INamedTypeSymbol
    class ValueRewriter(ITypeSymbol idType) : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            // Cover both the this.Value scenario
            if (node.Name.Identifier.Text == "Value")
                return ParenthesizedExpression(CastExpression(ParseTypeName(idType.ToFullName()), node));

            // As well as the Value.[Member] scenario
            if (node.Expression is IdentifierNameSyntax name && name.Identifier.Text == "Value")
                return node.WithExpression(ParenthesizedExpression(CastExpression(ParseTypeName(idType.ToFullName()), name)));

            return base.VisitMemberAccessExpression(node);
        }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var structIdNamespace = context.AnalyzerConfigOptionsProvider
            .Select((x, _) => x.GlobalOptions.TryGetValue("build_property.StructIdNamespace", out var ns) ? ns : "StructId");

        var known = context.CompilationProvider
            .Combine(structIdNamespace)
            .Select((x, _) => new KnownTypes(
                x.Right,
                // get string known type
                x.Left.GetTypeByMetadataName("System.String")!,
                x.Left.GetTypeByMetadataName($"{x.Right}.IStructId"),
                x.Left.GetTypeByMetadataName($"{x.Right}.IStructId`1"),
                x.Left.GetTypeByMetadataName($"{x.Right}.TStructIdAttribute"),
                x.Left.GetTypeByMetadataName($"{x.Right}.TStructIdAttribute`1")));

        var templates = context.CompilationProvider
            .SelectMany((x, _) => x.GetAllTypes(includeReferenced: true).OfType<INamedTypeSymbol>())
            .Combine(known)
            .Where(x =>
                // Ensure template is a partial record struct
                x.Left.TypeKind == TypeKind.Struct && x.Left.IsRecord &&
                // We can only work with templates where we have the actual syntax tree.
                x.Left.DeclaringSyntaxReferences.Length == 1 &&
                // The declaring syntax reference has a primary constructor with a single parameter named Value
                // This would be enforced by an analyzer/codefix pair.
                x.Left.DeclaringSyntaxReferences[0].GetSyntax() is TypeDeclarationSyntax declaration &&
                declaration.ParameterList?.Parameters.Count == 1 &&
                declaration.ParameterList.Parameters[0].Identifier.Text == "Value" &&
                // And we can locate the TStructIdAttribute type that should be applied to it.
                x.Right.TStructId != null && x.Right.TStructIdT != null &&
                x.Left.GetAttributes().Any(a => a.AttributeClass != null &&
                    // The attribute should either be the generic or regular TStructIdAttribute
                    (a.AttributeClass.Is(x.Right.TStructId) || a.AttributeClass.Is(x.Right.TStructIdT))))
            .Select((x, _) =>
            {
                var (structId, known) = x;
                var attribute = structId.GetAttributes().FirstOrDefault(a => a.AttributeClass != null && a.AttributeClass.Is(known.TStructIdT));
                if (attribute != null)
                    return new Template(structId, attribute.AttributeClass!.TypeArguments[0], attribute, known.StructIdNamespace, true);

                // If we don't have the generic attribute, infer the idType from the required 
                // primary constructor Value parameter type
                var idType = structId.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Value").Type;
                attribute = structId.GetAttributes().First(a => a.AttributeClass != null && a.AttributeClass.Is(known.TStructId));

                return new Template(structId, idType, attribute, known.StructIdNamespace, false);
            })
            .Collect();

        var ids = context.CompilationProvider
            .SelectMany((x, _) => x.Assembly.GetAllTypes().OfType<INamedTypeSymbol>())
            .Where(x => x.IsRecord && x.IsValueType && x.IsPartial())
            .Combine(known)
            .Where(x => x.Left.Is(x.Right.IStructId) || x.Left.Is(x.Right.IStructIdT))
            .Combine(templates)
            .Where(x =>
            {
                var ((id, known), templates) = x;
                var structId = id.AllInterfaces.FirstOrDefault(i => i.Is(known.IStructId) || i.Is(known.IStructIdT));
                return structId != null;
            })
            .SelectMany((x, _) =>
            {
                var ((id, known), templates) = x;
                // Locate the IStructId<TId> interface implemented by the id
                var structId = id.AllInterfaces.First(i => i.Is(known.IStructId) || i.Is(known.IStructIdT));
                var tid = structId.IsGenericType ? structId.TypeArguments[0] : known.String;
                // If the TId/Value implements or inherits from the template base type and/or its interfaces
                return templates
                    // check struct id's value type against the template's TId for compatibility
                    .Where(template =>
                        tid.Equals(template.TId, SymbolEqualityComparer.Default) ||
                        tid.Is(template.TId) ||
                        // If the template had a generic attribute, we'd be looking at an intermediate 
                        // type (typically TValue or TId) being used to define multiple constraints on 
                        // the struct id's value type, such as implementing multiple interfaces. In 
                        // this case, the tid would never equal or inherit from the template's TId, 
                        // but we want instead to check for base type compatibility plus all interfaces.
                        (template.IsGenericTId &&
                         // TId is a derived class of the template's TId base type (i.e. object or ValueType)
                         tid.Is(template.TId.BaseType) &&
                         // All template provided TId interfaces must be implemented by the struct id's TId
                         template.TId.AllInterfaces.All(iface =>
                            tid.AllInterfaces.Any(tface => tface.Is(iface)))))
                    .Select(template => new IdTemplate(id, template));
            });

        context.RegisterSourceOutput(ids, GenerateCode);
    }

    void GenerateCode(SourceProductionContext context, IdTemplate source)
    {
        var hintName = $"{source.StructId.ToFileName()}-{source.Template.TSelf.Name}.cs";
        var output = source.Template.NameExpr.Replace(source.Template.Text, source.StructId.Name);

        if (source.StructId.ContainingNamespace.Equals(source.StructId.ContainingModule.GlobalNamespace, SymbolEqualityComparer.Default))
        {
            // No need to tweak target namespace.
            context.AddSource(hintName, SourceText.From(output, Encoding.UTF8));
            return;
        }

        // parse template into a C# compilation unit
        var syntax = CSharpSyntaxTree.ParseText(output).GetCompilationUnitRoot();

        // if we got a ns, move all members after a file-scoped namespace declaration
        var members = syntax.Members;
        var fsns = FileScopedNamespaceDeclaration(ParseName(source.StructId.ContainingNamespace.ToDisplayString())
            .WithLeadingTrivia(Whitespace(" ")))
            .WithLeadingTrivia(LineFeed)
            .WithTrailingTrivia(LineFeed, LineFeed)
            .WithMembers(members);

        syntax = syntax.WithMembers(SingletonList<MemberDeclarationSyntax>(fsns));

        output = syntax.ToFullString();
        context.AddSource(hintName, SourceText.From(output, Encoding.UTF8));
    }
}
