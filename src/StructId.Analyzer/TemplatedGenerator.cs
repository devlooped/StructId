using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public partial class TemplatedGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Represents a template for struct ids.
    /// </summary>
    /// <param name="StructId">The struct id type, either IStructId or IStructId{T}.</param>
    /// <param name="TId">The type of value the struct id holds, such as Guid or string.</param>
    /// <param name="Template">The template to apply to it.</param>
    record IdTemplate(INamedTypeSymbol StructId, INamedTypeSymbol TId, Template Template);

    record Template(INamedTypeSymbol TSelf, INamedTypeSymbol TId, AttributeData Attribute, KnownTypes KnownTypes)
    {
        public INamedTypeSymbol? OriginalTId { get; init; }

        // A custom TId is a file-local type declaration.
        public bool IsLocalTId => OriginalTId?.IsFileLocal == true;

        public SyntaxNode Syntax { get; } = TSelf.DeclaringSyntaxReferences[0].GetSyntax().SyntaxTree.GetRoot();

        public bool NoString { get; } = new NoStringWalker().Accept(
            TSelf.DeclaringSyntaxReferences[0].GetSyntax().SyntaxTree.GetRoot());

        /// <summary>
        /// Checks the value type against the template's TId for compatibility
        /// </summary>
        public bool AppliesTo(INamedTypeSymbol valueType)
        {
            if (NoString && valueType.Equals(KnownTypes.String, SymbolEqualityComparer.Default))
                return false;

            if (valueType.Equals(TId, SymbolEqualityComparer.Default))
                return true;

            if (valueType.Is(TId))
                return true;

            // If the template had a generic attribute, we'd be looking at an intermediate 
            // type (typically TValue or TId) being used to define multiple constraints on 
            // the struct id's value type, such as implementing multiple interfaces. In 
            // this case, the tid would never equal or inherit from the template's TId, 
            // but we want instead to check for base type compatibility plus all interfaces.
            return IsLocalTId &&
                 // TId is a derived class of the template's TId base type (i.e. object or ValueType)
                 valueType.Is(TId.BaseType) &&
                 // All template provided TId interfaces must be implemented by the struct id's TId
                 TId.AllInterfaces.All(iface =>
                    valueType.AllInterfaces.Any(tface => tface.Is(iface)));
        }

        class NoStringWalker : CSharpSyntaxWalker
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
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var structIdNamespace = context.AnalyzerConfigOptionsProvider.GetStructIdNamespace();

        var known = context.CompilationProvider
            .Combine(structIdNamespace)
            .Select((x, _) => new KnownTypes(x.Left, x.Right));

        var templates = context.CompilationProvider
            .SelectMany((x, _) => x.GetAllTypes(includeReferenced: true).OfType<INamedTypeSymbol>())
            .Where(x =>
                // Ensure template is a file-local partial record struct
                x.TypeKind == TypeKind.Struct && x.IsRecord && x.IsFileLocal &&
                // We can only work with templates where we have the actual syntax tree.
                x.DeclaringSyntaxReferences.Any(
                    // And we can locate the TStructIdAttribute type that should be applied to it.
                    r => r.GetSyntax() is TypeDeclarationSyntax declaration && x.GetAttributes().Any(
                        a => a.IsStructIdTemplate())))
            .Combine(known)
            .Select((x, cancellation) =>
            {
                var (structId, known) = x;
                // We infer the idType from the required primary constructor Value parameter type
                var idType = (INamedTypeSymbol)structId.GetMembers().OfType<IPropertySymbol>().First(p => p.Name == "Value").Type;
                var attribute = structId.GetAttributes().First(a => a.AttributeClass != null && a.AttributeClass.Is(known.TStructId));

                // The id type isn't declared in the same file, so we don't do anything fancy with it.
                if (idType.DeclaringSyntaxReferences.Length == 0)
                    return new Template(structId, idType, attribute, known);

                // Otherwise, the idType is a file-local type with a single interface
                var type = idType.DeclaringSyntaxReferences[0].GetSyntax(cancellation) as TypeDeclarationSyntax;
                var iface = type?.BaseList?.Types.FirstOrDefault()?.Type;
                if (type == null || iface == null)
                    return new Template(structId, idType, attribute, known) { OriginalTId = idType };

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
                    .Where(template => template.AppliesTo(tid))
                    .Select(template => new IdTemplate(id, tid, template));
            });

        context.RegisterSourceOutput(ids, GenerateCode);
    }

    void GenerateCode(SourceProductionContext context, IdTemplate source)
    {
        var templateFile = Path.GetFileNameWithoutExtension(source.Template.Syntax.SyntaxTree.FilePath);
        var hintName = $"{source.StructId.ToFileName()}/{templateFile}.cs";

        var applied = source.Template.Syntax.Apply(source.StructId);
        var output = applied.ToFullString();

        context.AddSource(hintName, SourceText.From(output, Encoding.UTF8));
    }
}
