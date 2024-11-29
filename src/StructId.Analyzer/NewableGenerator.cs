using System;
using Microsoft.CodeAnalysis;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class NewableGenerator() : BaseGenerator(
    "System.Object",
    ThisAssembly.Resources.Templates.Newable.Text,
    ThisAssembly.Resources.Templates.NewableT.Text,
    ReferenceCheck.TypeExists)
{
    protected override IncrementalValuesProvider<TemplateArgs> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<TemplateArgs> source)
    {
        var args = base.OnInitialize(context, source);

        context.RegisterSourceOutput(
            args.Where(x => x.ValueType.ToFullName() == "System.Guid"),
            GenerateGuidCode);

        return args;
    }

    void GenerateGuidCode(SourceProductionContext context, TemplateArgs args) => AddFromTemplate(
        context, args, $"{args.StructId.ToFileName()}.Guid.cs", ThisAssembly.Resources.Templates.NewableGuid.Text);
}