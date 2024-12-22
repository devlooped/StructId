using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StructId;

/// <summary>
/// Represents a templatized value type of a struct ids.
/// </summary>
/// <param name="TValue">The type of value the a struct id holds, such as Guid or string.</param>
/// <param name="Template">The template to apply to it.</param>
record TemplatizedTValue(INamedTypeSymbol TValue, TValueTemplate Template)
{
    SyntaxNode? applied;

    public SyntaxNode Syntax => (applied ??= Template.Syntax.ApplyValue(TValue));

    public TypeDeclarationSyntax Declaration => Syntax
        .DescendantNodes()
        .OfType<TypeDeclarationSyntax>()
        .First();

    public string TypeName => Declaration.Identifier.Text;

    public string Render() => Declaration.ToFullString();
}

/// <summary>
/// Represents a generic file-local template that applies to TValues that match the template 
/// constraints.
/// </summary>
/// <param name="TTemplate">The declared symbol of the template in the compilation.</param>
/// <param name="KnownTypes">Useful known types for use when applying the template.</param>
record TValueTemplate(INamedTypeSymbol TTemplate, KnownTypes KnownTypes)
{
    /// <summary>
    /// Syntax root of the file declaring the template.
    /// </summary>
    public SyntaxNode Syntax { get; } = TTemplate.DeclaringSyntaxReferences[0].GetSyntax().SyntaxTree.GetRoot();

    /// <summary>
    /// Whether the template should not be applied to string value types.
    /// </summary>
    /// <remarks>
    /// Since strings implement also a bunch of interfaces, an easy way to exclude them 
    /// from matching a struct value template that has a restriction on just 
    /// </remarks>
    public bool NoString { get; } = new NoStringSyntaxWalker().Accept(
        TTemplate.DeclaringSyntaxReferences[0].GetSyntax().SyntaxTree.GetRoot());

    public INamedTypeSymbol TValue => Syntax.DescendantNodes()
        .OfType<TypeDeclarationSyntax>()
        .Select(x => KnownTypes.Compilation.GetSemanticModel(Syntax.SyntaxTree).GetDeclaredSymbol(x))
        .FirstOrDefault(x => x != null && x.Name == "TValue") ?? TTemplate;

    /// <summary>
    /// Checks the value type against the template's TValue for compatibility
    /// </summary>
    public bool AppliesTo(INamedTypeSymbol valueType)
    {
        if (NoString && valueType.Equals(KnownTypes.String, SymbolEqualityComparer.Default))
            return false;

        if (valueType.Equals(TValue, SymbolEqualityComparer.Default))
            return true;

        if (valueType.Is(TValue))
            return true;

        // If the template had a generic attribute, we'd be looking at an intermediate 
        // type (typically TValue) being used to define multiple constraints on 
        // the struct id's value type, such as implementing multiple interfaces. In 
        // this case, the tid would never equal or inherit from the template's TValue, 
        // but we want instead to check for base type compatibility plus all interfaces.
        return TValue.IsFileLocal &&
             // TValue is a derived class of the template's TValue base type (i.e. object or ValueType)
             valueType.Is(TValue.BaseType) &&
             // All template provided TValue interfaces must be implemented by the struct id's TValue
             TValue.AllInterfaces.All(iface =>
                valueType.AllInterfaces.Any(tface => tface.Is(iface)));
    }
}

static class TemplatizedTValueExtensions
{
    /// <summary>
    /// Gets all instantiations of TValue templates that apply to the struct ids in the compilation.
    /// </summary>
    public static IncrementalValuesProvider<TemplatizedTValue> SelectTemplatizedValues(this IncrementalGeneratorInitializationContext context)
    {
        var structIdNamespace = context.AnalyzerConfigOptionsProvider.GetStructIdNamespace();

        var known = context.CompilationProvider
            .Combine(structIdNamespace)
            .Select((x, _) => new KnownTypes(x.Left, x.Right));

        var templates = context.CompilationProvider
            .SelectMany((x, _) => x.GetAllTypes(includeReferenced: true).OfType<INamedTypeSymbol>())
            .Where(x =>
                // Ensure template is a file-local type
                x.IsFileLocal &&
                // We can only work with templates where we have the actual syntax tree.
                x.DeclaringSyntaxReferences.Any(
                    // And we can locate the TStructIdAttribute type that should be applied to it.
                    r => r.GetSyntax() is TypeDeclarationSyntax declaration && x.GetAttributes().Any(
                        a => a.IsValueTemplate())))
            .Combine(known)
            .Select((x, cancellation) => new TValueTemplate(x.Left, x.Right))
            .Collect();

        var values = context.CompilationProvider
            .SelectMany((x, _) => x.Assembly.GetAllTypes().OfType<INamedTypeSymbol>())
            .Where(x => x.IsRecord && x.IsValueType && x.IsPartial())
            .Combine(known)
            .Where(x => x.Left.Is(x.Right.IStructIdT))
            .Combine(templates)
            .SelectMany((x, _) =>
            {
                var ((id, known), templates) = x;
                // Locate the IStructId<TValue> interface implemented by the id
                var structId = id.AllInterfaces.First(i => i.Is(known.IStructIdT));
                var tvalue = (INamedTypeSymbol)structId.TypeArguments[0];
                return templates
                    .Where(template => template.AppliesTo(tvalue))
                    .Select(template => new TemplatizedTValue(tvalue, template));
            });

        return values;
    }
}
