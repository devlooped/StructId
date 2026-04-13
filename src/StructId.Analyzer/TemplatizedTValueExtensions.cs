using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StructId;

static class TemplatizedTValueExtensions
{
    /// <summary>
    /// Gets all instantiations of TValue templates that apply to the struct ids in the compilation.
    /// Returns string-based models with no ISymbol/Compilation references for proper incremental caching.
    /// </summary>
    public static IncrementalValuesProvider<TemplatizedValueOutput> SelectTemplatizedValues(this IncrementalGeneratorInitializationContext context)
    {
        // Discover TValue templates from all types (including referenced assemblies)
        // Templates are file-local types with [TValue] attribute in the StructId package.
        // We use CompilationProvider.Select to extract cacheable string-based models.
        var templates = context.CompilationProvider
            .Select(static (c, _) =>
            {
                var models = c.GetAllTypes(includeReferenced: true)
                    .OfType<INamedTypeSymbol>()
                    .Where(x =>
                        x.IsFileLocal &&
                        x.DeclaringSyntaxReferences.Any(
                            r => r.GetSyntax() is TypeDeclarationSyntax) &&
                        x.GetAttributes().Any(a => a.IsValueTemplate()))
                    .Select(x => ModelExtractors.ExtractTValueTemplateModel(x, c))
                    .Where(x => x != null)
                    .Select(x => x!.Value)
                    .ToImmutableArray();
                return new EquatableArray<TValueTemplateModel>(models);
            })
            .WithTrackingName(TrackingNames.TValueTemplates);

        // Discover struct ID value types via syntax predicate
        var structIds = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is RecordDeclarationSyntax rds &&
                    rds.BaseList?.Types.Any(t => t.Type.ToString().Contains("IStructId")) == true,
                transform: static (ctx, ct) =>
                {
                    return ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is INamedTypeSymbol symbol ? ModelExtractors.ExtractStructIdModel(symbol) : null;
                })
            .Where(static x => x != null)
            .Select(static (x, _) => x!.Value)
            .WithTrackingName(TrackingNames.TValueValues);

        // Cross-product: for each struct ID, find matching TValue templates and apply them
        return structIds.Combine(templates)
            .SelectMany(static (x, _) =>
            {
                var (model, templates) = x;
                return templates.AsImmutableArray()
                    .Where(t => t.AppliesTo(model))
                    .Select(t =>
                    {
                        // Apply the template to produce the output code
                        var applied = CodeTemplate.Apply(t.TemplateSyntaxText, model.ValueTypeFullName);
                        var tree = CSharpSyntaxTree.ParseText(applied);
                        var decl = tree.GetRoot().DescendantNodes()
                            .OfType<TypeDeclarationSyntax>().First();
                        return new TemplatizedValueOutput(
                            TTemplateFullName: t.TTemplateFullName,
                            TTemplateAllBaseTypes: t.TTemplateAllBaseTypes,
                            TTemplateAllInterfaces: t.TTemplateAllInterfaces,
                            ValueTypeFullName: model.ValueTypeFullName,
                            AppliedTypeName: decl.Identifier.Text,
                            AppliedCode: decl.ToFullString());
                    });
            });
    }
}
