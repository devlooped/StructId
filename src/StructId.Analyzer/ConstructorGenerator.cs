using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class ConstructorGenerator() : BaseGenerator(
    "System.Object",
    ThisAssembly.Resources.Templates.Constructor.Text,
    ThisAssembly.Resources.Templates.ConstructorT.Text,
    ReferenceCheck.TypeExists)
{
    // NOTE: since we only emit the ctor if the struct doesn't already have one, 
    // we cannot switch this to the simpler compiled templates, which don't have conditional logic
    protected override IncrementalValuesProvider<TemplateArgs> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<TemplateArgs> source)
        => base.OnInitialize(context, source.Where(x
            => x.StructId.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<TypeDeclarationSyntax>().All(s => s.ParameterList == null)));
}