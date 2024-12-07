using System.Collections.Concurrent;
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
public partial class TemplatedGenerator : IIncrementalGenerator
{
    static Regex TSelfExpr = new($@"\bTSelf\b", RegexOptions.Compiled | RegexOptions.Multiline);

    static Regex TIdExpr = new($@"\bTId\b", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Provides access to some common types and properties used in the compilation.
    /// </summary>
    /// <param name="Compilation">The compilation used to resolve the known types.</param>
    /// <param name="StructIdNamespace">The namespace for StructId types.</param>
    record KnownTypes(Compilation Compilation, string StructIdNamespace)
    {
        /// <summary>
        /// System.String
        /// </summary>
        public INamedTypeSymbol String { get; } = Compilation.GetTypeByMetadataName("System.String")!;
        /// <summary>
        /// StructId.IStructId
        /// </summary>
        public INamedTypeSymbol? IStructId { get; } = Compilation.GetTypeByMetadataName($"{StructIdNamespace}.IStructId");
        /// <summary>
        /// StructId.IStructId{T}
        /// </summary>
        public INamedTypeSymbol? IStructIdT { get; } = Compilation.GetTypeByMetadataName($"{StructIdNamespace}.IStructId`1");
        /// <summary>
        /// StructId.TStructIdAttribute
        /// </summary>
        public INamedTypeSymbol? TStructId { get; } = Compilation.GetTypeByMetadataName($"{StructIdNamespace}.TStructIdAttribute");
        /// <summary>
        /// StructId.TStructIdAttribute{T}
        /// </summary>
        public INamedTypeSymbol? TStructIdT { get; } = Compilation.GetTypeByMetadataName($"{StructIdNamespace}.TStructIdAttribute`1");
    }

    /// <summary>
    /// Represents a template for struct ids.
    /// </summary>
    /// <param name="StructId">The struct id type, either IStructId or IStructId{T}.</param>
    /// <param name="TId">The type of value the struct id holds, such as Guid or string.</param>
    /// <param name="Template">The template to apply to it.</param>
    record IdTemplate(INamedTypeSymbol StructId, INamedTypeSymbol TId, Template Template);

    record Template(INamedTypeSymbol TSelf, INamedTypeSymbol TId, AttributeData Attribute, KnownTypes KnownTypes)
    {
        string? code;

        public INamedTypeSymbol? OriginalTId { get; init; }

        // A custom TId is a file-local type declaration.
        public bool IsLocalTId => OriginalTId?.DeclaringSyntaxReferences
            .All(x => x.GetSyntax() is TypeDeclarationSyntax decl && decl.Modifiers.Any(m => m.IsKind(SyntaxKind.FileKeyword))) == true;

        public string Text
        {
            get => code ??= GetTemplateCode(TSelf, TId, OriginalTId, Attribute, KnownTypes);
        }

        static string GetTemplateCode(INamedTypeSymbol self, INamedTypeSymbol id,
            INamedTypeSymbol? originalId, AttributeData attribute, KnownTypes known)
        {
            if (self.DeclaringSyntaxReferences[0].GetSyntax() is not TypeDeclarationSyntax declaration)
                return "";

            // Remove the TId/TValue if present in the same syntax tree.
            var toremove = id.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).ToList();
            // The target id might not be the same as the original id (which can be a local TId)
            if (originalId != null)
                toremove.AddRange(originalId.DeclaringSyntaxReferences.Select(x => x.GetSyntax()));

            // Also the [TStructId<T>] attribute applied to the template itself
            if (attribute.ApplicationSyntaxReference?.GetSyntax().FirstAncestorOrSelf<AttributeListSyntax>() is { } attr)
                toremove.Add(attr);
            // And the primary constructor if present, since that's generated for the struct id already
            if (declaration.ParameterList != null)
                toremove.Add(declaration.ParameterList);

            var root = declaration.SyntaxTree
                .GetRoot()
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
                root = root.ReplaceNode(ns!, ns!.WithName(ParseName(known.StructIdNamespace)));
            else if (nsname != known.StructIdNamespace)
                usings.Add(UsingDirective(ParseName(known.StructIdNamespace)).NormalizeWhitespace());

            // deduplicate usings just in case
            var unique = new HashSet<string>();
            root = root.ReplaceNodes(usings, (old, _) =>
            {
                // replace 'StructId' > StructIdNamespace
                if (old.Name?.ToString() == "StructId")
                {
                    unique.Add(known.StructIdNamespace);
                    return old.WithName(ParseName(known.StructIdNamespace));
                }

                if (unique.Add(old.Name?.ToString() ?? ""))
                    return old;

                return null!;
            });

            var code = root.SyntaxTree.GetRoot().ToFullString().Trim();

            return code;
        }
    }

    class ValueTypeRewriter(INamedTypeSymbol originalType, INamedTypeSymbol targetType) : CSharpSyntaxRewriter
    {
        // rewrite references to the original type with the target type
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.Text == originalType.Name)
                return IdentifierName(targetType.ToFullName());

            return base.VisitIdentifierName(node);
        }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var structIdNamespace = context.AnalyzerConfigOptionsProvider.GetStructIdNamespace();

        var known = context.CompilationProvider
            .Combine(structIdNamespace)
            .Select((x, _) => new KnownTypes(x.Left, x.Right));

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
            .Select((x, cancellation) =>
            {
                var (structId, known) = x;
                var attribute = structId.GetAttributes().FirstOrDefault(a => a.AttributeClass != null && a.AttributeClass.Is(known.TStructIdT));
                if (attribute != null && attribute.AttributeClass!.TypeArguments[0] is INamedTypeSymbol attrType)
                    return new Template(structId, attrType, attribute, known);

                // If we don't have the generic attribute, infer the idType from the required 
                // primary constructor Value parameter type
                var idType = (INamedTypeSymbol)structId.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Value").Type;
                attribute = structId.GetAttributes().First(a => a.AttributeClass != null && a.AttributeClass.Is(known.TStructId));

                // The id type isn't declared in the same file, so we don't do anything fancy with it.
                if (idType.DeclaringSyntaxReferences.Length == 0)
                    return new Template(structId, idType, attribute, known);

                // Otherwise, the idType is a file-local type with a single interface
                var type = idType.DeclaringSyntaxReferences[0].GetSyntax(cancellation) as TypeDeclarationSyntax;
                var iface = type?.BaseList?.Types.FirstOrDefault()?.Type;
                if (type == null || iface == null)
                    return new Template(structId, idType, attribute, known);

                if (x.Right.Compilation.GetSemanticModel(type.SyntaxTree).GetSymbolInfo(iface).Symbol is not INamedTypeSymbol ifaceType)
                    return new Template(structId, idType, attribute, known);

                // if the interface is a generic type with a single type argument that is the same as the idType
                // make it an unbound generic type. We'll bind it to the actual idType later at template render time.
                if (ifaceType.IsGenericType && ifaceType.TypeArguments.Length == 1 && ifaceType.TypeArguments[0].Equals(idType, SymbolEqualityComparer.Default))
                    ifaceType = ifaceType.ConstructUnboundGenericType();

                return new Template(structId, ifaceType, attribute, known)
                {
                    OriginalTId = idType
                };
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
                var tid = structId.IsGenericType ? (INamedTypeSymbol)structId.TypeArguments[0] : known.String;
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
                        (template.IsLocalTId &&
                         // TId is a derived class of the template's TId base type (i.e. object or ValueType)
                         tid.Is(template.TId.BaseType) &&
                         // All template provided TId interfaces must be implemented by the struct id's TId
                         template.TId.AllInterfaces.All(iface =>
                            tid.AllInterfaces.Any(tface => tface.Is(iface)))))
                    .Select(template => new IdTemplate(id, tid, template));
            });

        context.RegisterSourceOutput(ids, GenerateCode);
    }

    void GenerateCode(SourceProductionContext context, IdTemplate source)
    {
        var hintName = $"{source.StructId.ToFileName()}/{source.Template.TId.ToFileName()}.cs";
        var output = TIdExpr.Replace(
            TSelfExpr.Replace(source.Template.Text, source.StructId.Name),
            source.TId.ToFullName());

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
