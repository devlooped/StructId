using Microsoft.CodeAnalysis;

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
    protected override IncrementalValuesProvider<StructIdModel> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<StructIdModel> source)
        => base.OnInitialize(context, source.Where(x => !x.HasParameterList));
}