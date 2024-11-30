using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

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
    protected record struct TemplateArgs(string StructIdNamespace, INamedTypeSymbol StructId, INamedTypeSymbol ValueType, INamedTypeSymbol ReferenceType, INamedTypeSymbol StringType);

    public virtual void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targetNamespace = context.AnalyzerConfigOptionsProvider
            .Select((x, _) => x.GlobalOptions.TryGetValue("build_property.StructIdNamespace", out var ns) ? ns : "StructId");

        // Locate the required types
        var types = context.CompilationProvider
            .Select((x, _) => (ReferenceType: x.GetTypeByMetadataName(referenceType), StringType: x.GetTypeByMetadataName("System.String")));

        var ids = context.CompilationProvider
            .SelectMany((x, _) => x.Assembly.GetAllTypes().OfType<INamedTypeSymbol>())
            .Where(x => x.IsStructId())
            .Where(x => x.IsPartial());

        var combined = ids.Combine(types)
            // NOTE: we never generate for compilations that don't have the specified value interface type
            .Where(x => x.Right.ReferenceType != null || x.Right.StringType == null)
            .Combine(targetNamespace)
            .Select((x, _) =>
            {
                var ((structId, (referenceType, stringType)), targetNamespace) = x;

                // The value type is either a generic type argument for IStructId<T>, or the string type 
                // for the non-generic IStructId
                var valueType = structId.AllInterfaces
                    .First(x => x.Name == "IStructId")
                    .TypeArguments.OfType<INamedTypeSymbol>().FirstOrDefault() ??
                    stringType!;

                return new TemplateArgs(targetNamespace, structId, valueType, referenceType!, stringType!);
            });

        if (referenceCheck == ReferenceCheck.ValueIsType)
            combined = combined.Where(x => x.ValueType.Is(x.ReferenceType));

        combined = OnInitialize(context, combined);

        context.RegisterImplementationSourceOutput(combined, GenerateCode);
    }

    protected virtual IncrementalValuesProvider<TemplateArgs> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<TemplateArgs> source) => source;

    void GenerateCode(SourceProductionContext context, TemplateArgs args) => AddFromTemplate(
        context, args, $"{args.StructId.ToFileName()}.cs",
        args.ValueType.Equals(args.StringType, SymbolEqualityComparer.Default) ? stringTemplate : typeTemplate);

    protected static void AddFromTemplate(SourceProductionContext context, TemplateArgs args, string hintName, string template)
    {
        var ns = args.StructId.ContainingNamespace.Equals(args.StructId.ContainingModule.GlobalNamespace, SymbolEqualityComparer.Default)
            ? null
            : args.StructId.ContainingNamespace.ToDisplayString();

        // replace tokens in the template
        var replaced = template
            // Adjust to current target namespace
            .Replace("namespace StructId;", $"namespace {args.StructIdNamespace};")
            .Replace("using StructId;", $"using {args.StructIdNamespace};")
            // Simple names suffices since we emit a partial in the same namespace
            .Replace("TSelf", args.StructId.Name)
            .Replace("Self", args.StructId.Name)
            .Replace("TId", args.ValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        // parse template into a C# compilation unit
        var syntax = CSharpSyntaxTree.ParseText(replaced).GetCompilationUnitRoot();

        // if we got a ns, move all members after a file-scoped namespace declaration
        if (ns != null)
        {
            var members = syntax.Members;
            var fsns = FileScopedNamespaceDeclaration(ParseName(ns).WithLeadingTrivia(Whitespace(" ")))
                .WithLeadingTrivia(LineFeed)
                .WithTrailingTrivia(LineFeed)
                .WithMembers(members);
            syntax = syntax.WithMembers(SingletonList<MemberDeclarationSyntax>(fsns));
        }

        var output = syntax.ToFullString();

        context.AddSource(hintName, SourceText.From(output, Encoding.UTF8));
    }
}
