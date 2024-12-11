using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
    SyntaxNode? stringSyntax;
    SyntaxNode? typedSyntax;

    protected record struct TemplateArgs(INamedTypeSymbol TSelf, INamedTypeSymbol TId, INamedTypeSymbol ReferenceType, KnownTypes KnownTypes);

    public virtual void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var structIdNamespace = context.AnalyzerConfigOptionsProvider.GetStructIdNamespace();

        var known = context.CompilationProvider
            .Combine(structIdNamespace)
            .Select((x, _) => new KnownTypes(x.Left, x.Right));

        // Locate the required type
        var types = context.CompilationProvider
            .Select((x, _) => x.GetTypeByMetadataName(referenceType));

        var ids = context.CompilationProvider
            .SelectMany((x, _) => x.Assembly.GetAllTypes().OfType<INamedTypeSymbol>())
            .Where(x => x.IsStructId())
            .Where(x => x.IsPartial());

        var combined = ids.Combine(types)
            // NOTE: we never generate for compilations that don't have the specified value interface type
            .Where(x => x.Right != null)
            .Combine(known)
            .Select((x, _) =>
            {
                var ((structId, referenceType), known) = x;

                // The value type is either a generic type argument for IStructId<T>, or the string type 
                // for the non-generic IStructId
                var valueType = structId.AllInterfaces
                    .First(x => x.Name == "IStructId")
                    .TypeArguments.OfType<INamedTypeSymbol>().FirstOrDefault() ??
                    known.String;

                return new TemplateArgs(structId, valueType, referenceType!, known);
            });

        if (referenceCheck == ReferenceCheck.ValueIsType)
            combined = combined.Where(x => x.TId.Is(x.ReferenceType));

        combined = OnInitialize(context, combined);

        context.RegisterImplementationSourceOutput(combined, GenerateCode);
    }

    protected virtual IncrementalValuesProvider<TemplateArgs> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<TemplateArgs> source) => source;

    void GenerateCode(SourceProductionContext context, TemplateArgs args) => AddFromTemplate(
        context, args, $"{args.TSelf.ToFileName()}.cs",
        args.TId.Equals(args.KnownTypes.String, SymbolEqualityComparer.Default) ?
            (stringSyntax ??= CodeTemplate.Parse(stringTemplate, args.KnownTypes.Compilation.GetParseOptions())) :
            (typedSyntax ??= CodeTemplate.Parse(typeTemplate, args.KnownTypes.Compilation.GetParseOptions())));

    protected static void AddFromTemplate(SourceProductionContext context, TemplateArgs args, string hintName, SyntaxNode template)
    {
        var applied = template.Apply(args.TSelf);
        var output = applied.ToFullString();

        context.AddSource(hintName, SourceText.From(output, Encoding.UTF8));
    }
}
