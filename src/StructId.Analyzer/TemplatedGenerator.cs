using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public partial class TemplatedGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Discover [TStructId] templates from all types (including referenced assemblies)
        var templates = context.CompilationProvider
            .Select(static (c, _) =>
            {
                var models = c.GetAllTypes(includeReferenced: true)
                    .OfType<INamedTypeSymbol>()
                    .Where(x =>
                        x.TypeKind == TypeKind.Struct && x.IsRecord && x.IsFileLocal &&
                        x.DeclaringSyntaxReferences.Any(
                            r => r.GetSyntax() is TypeDeclarationSyntax) &&
                        x.GetAttributes().Any(a => a.IsStructIdTemplate()))
                    .Select(x => ModelExtractors.ExtractTemplateModel(x, c))
                    .Where(x => x != null)
                    .Select(x => x!.Value)
                    .ToImmutableArray();
                return new EquatableArray<TemplateModel>(models);
            })
            .WithTrackingName(TrackingNames.Templates);

        // Discover struct IDs via syntax predicate + semantic transform
        var ids = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is RecordDeclarationSyntax rds &&
                    rds.BaseList?.Types.Any(t => t.Type.ToString().Contains("IStructId")) == true,
                transform: static (ctx, ct) =>
                {
                    if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is not INamedTypeSymbol symbol)
                        return null;
                    // Exclude template types themselves
                    if (symbol.IsValueTemplate() || symbol.IsStructIdTemplate())
                        return null;
                    return ModelExtractors.ExtractStructIdModel(symbol);
                })
            .Where(static x => x != null)
            .Select(static (x, _) => x!.Value);

        // Cross-product: for each struct ID, find matching templates
        var templatized = ids.Combine(templates)
            .SelectMany(static (x, _) =>
            {
                var (model, templates) = x;
                return templates.AsImmutableArray()
                    .Where(t => t.AppliesTo(model))
                    .Select(t => (StructId: model, Template: t));
            })
            .WithTrackingName(TrackingNames.TemplatizedStructIds);

        context.RegisterSourceOutput(templatized, GenerateCode);
    }

    static void GenerateCode(SourceProductionContext context, (StructIdModel StructId, TemplateModel Template) source)
    {
        var templateFile = Path.GetFileNameWithoutExtension(source.Template.TemplateFilePath);
        var hintName = $"{source.StructId.FileName}/{templateFile}.cs";

        var output = CodeTemplate.Apply(source.Template.TemplateSyntaxText, source.StructId.TypeName, source.StructId.ValueTypeFullName, source.StructId.Namespace, source.StructId.CoreNamespace);

        context.AddSource(hintName, SourceText.From(output, Encoding.UTF8));
    }
}
