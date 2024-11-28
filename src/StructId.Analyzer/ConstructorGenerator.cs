using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class ConstructorGenerator() : TemplateGenerator(
    "System.Object",
    ThisAssembly.Resources.Templates.Constructor.Text,
    ThisAssembly.Resources.Templates.ConstructorT.Text,
    ReferenceCheck.TypeExists)
{
    protected override IncrementalValuesProvider<TemplateArgs> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<TemplateArgs> source)
        => base.OnInitialize(context, source.Where(x
            => x.StructId.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<TypeDeclarationSyntax>().All(s => s.ParameterList == null)));
}