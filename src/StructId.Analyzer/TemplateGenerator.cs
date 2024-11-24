using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace StructId;

public enum TypeCheck
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

public abstract class TemplateGenerator(string valueType, string stringTemplate, string typeTemplate, TypeCheck interfaceCheck = TypeCheck.ValueIsType) : IIncrementalGenerator
{
    record struct TemplateArgs(string TargetNamespace, INamedTypeSymbol StructId, INamedTypeSymbol ValueType, INamedTypeSymbol InterfaceType, INamedTypeSymbol StringType);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targetNamespace = context.AnalyzerConfigOptionsProvider
            .Select((x, _) => x.GlobalOptions.TryGetValue("build_property.StructIdNamespace", out var ns) ? ns : "StructId");

        // Locate the required types
        var types = context.CompilationProvider
            .Select((x, _) => (InterfaceType: x.GetTypeByMetadataName(valueType), StringType: x.GetTypeByMetadataName("System.String")));

        var ids = context.CompilationProvider
            .SelectMany((x, _) => x.Assembly.GetAllTypes().OfType<INamedTypeSymbol>())
            .Where(x => x.IsStructId())
            .Where(x => x.IsPartial());

        var combined = ids.Combine(types)
            // NOTE: we never generate for compilations that don't have the specified value interface type
            .Where(x => x.Right.InterfaceType != null || x.Right.StringType == null)
            .Combine(targetNamespace)
            .Select((x, _) =>
            {
                var ((structId, (interfaceType, stringType)), targetNamespace) = x;

                // The value type is either a generic type argument for IStructId<T>, or the string type 
                // for the non-generic IStructId
                var valueType = structId.AllInterfaces
                    .First(x => x.Name == "IStructId")
                    .TypeArguments.OfType<INamedTypeSymbol>().FirstOrDefault() ??
                    stringType!;

                return new TemplateArgs(targetNamespace, structId, valueType, interfaceType!, stringType!);
            });

        if (interfaceCheck == TypeCheck.ValueIsType)
            combined = combined.Where(x => x.ValueType.Is(x.InterfaceType));

        context.RegisterImplementationSourceOutput(combined, GenerateCode);
    }

    void GenerateCode(SourceProductionContext context, TemplateArgs args)
    {
        var ns = args.StructId.ContainingNamespace.Equals(args.StructId.ContainingModule.GlobalNamespace, SymbolEqualityComparer.Default)
            ? null
            : args.StructId.ContainingNamespace.ToDisplayString();

        var template = args.ValueType.Equals(args.StringType, SymbolEqualityComparer.Default)
            ? stringTemplate : typeTemplate;

        // replace tokens in the template
        template = template
            // Adjust to current target namespace
            .Replace("namespace StructId;", $"namespace {args.TargetNamespace};")
            .Replace("using StructId;", $"using {args.TargetNamespace};")
            // Simple names suffices since we emit a partial in the same namespace
            .Replace("TStruct", args.StructId.Name)
            .Replace("SStruct", args.StructId.Name)
            .Replace("TValue", args.ValueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

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

        context.AddSource($"{args.StructId.ToFileName()}.cs", SourceText.From(parseable.ToFullString(), Encoding.UTF8));
    }
}
