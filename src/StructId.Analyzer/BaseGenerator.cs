using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StructId;

public enum ReferenceCheck
{
    /// <summary>
    /// The check involves ensuring the type exists in the compilation.
    /// </summary>
    TypeExists,
    /// <summary>
    /// In addition to <see cref="TypeExists"/>, the check involves ensuring the type implements the interface.
    /// </summary>
    ValueIsType,
}

public abstract class BaseGenerator(string referenceType, string stringTemplate, string typeTemplate, ReferenceCheck referenceCheck = ReferenceCheck.ValueIsType) : IIncrementalGenerator
{
    public virtual void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get the reference type's original definition display name for matching
        var refType = context.CompilationProvider
            .Select((x, _) => x.GetTypeByMetadataName(referenceType)?.OriginalDefinition.ToFullName())
            .WithTrackingName(TrackingNames.ReferenceType);

        // Discover struct IDs via syntax predicate + semantic transform
        var ids = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) =>
                node is RecordDeclarationSyntax rds &&
                rds.BaseList?.Types.Any(t => t.Type.ToString().Contains("IStructId")) == true,
            transform: static (ctx, ct) =>
                ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is INamedTypeSymbol symbol ? ModelExtractors.ExtractStructIdModel(symbol) : null)
            .Where(static x => x != null)
            .Select(static (x, _) => x!.Value)
            .WithTrackingName(TrackingNames.StructIds);

        // Combine with reference type existence check
        var combined = ids.Combine(refType)
            .Where(static x => x.Right != null);

        // For ValueIsType, additionally filter to struct ids whose value type implements the reference type
        if (referenceCheck == ReferenceCheck.ValueIsType)
            combined = combined.Where(static x => x.Left.ValueTypeAllInterfaces.Contains(x.Right!));

        var models = combined
            .Select(static (x, _) => x.Left)
            .WithTrackingName(TrackingNames.Combined);

        models = OnInitialize(context, models);

        context.RegisterImplementationSourceOutput(models, GenerateCode);
    }

    protected virtual IncrementalValuesProvider<StructIdModel> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<StructIdModel> source) => source;

    void GenerateCode(SourceProductionContext context, StructIdModel model)
        => AddFromTemplate(context, model, $"{model.FileName}.cs", SelectTemplate(model));

    protected virtual string SelectTemplate(StructIdModel model)
        => model.IsStringValue ? stringTemplate : typeTemplate;

    protected static void AddFromTemplate(SourceProductionContext context, StructIdModel model, string hintName, string templateText)
    {
        var output = CodeTemplate.Apply(templateText, model.TypeName, model.ValueTypeFullName, model.Namespace, model.CoreNamespace);
        context.AddSource(hintName, SourceText.From(output, Encoding.UTF8));
    }
}
