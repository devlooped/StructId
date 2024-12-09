using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Scriban;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class DapperGenerator() : BaseGenerator(
    "Dapper.SqlMapper+TypeHandler`1", "", "", ReferenceCheck.TypeExists)
{
    static readonly Template template = Template.Parse(ThisAssembly.Resources.DapperExtensions.Text);

    protected override IncrementalValuesProvider<TemplateArgs> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<TemplateArgs> source)
    {
        var supported = source.Where(x => x.ValueType.ToFullName() switch
        {
            "System.String" => true,
            "System.Guid" => true,
            "System.Int32" => true,
            "System.Int64" => true,
            "string" => true,
            "int" => true,
            "long" => true,
            _ => false
        });

        context.RegisterSourceOutput(supported.Collect(), GenerateHandlers);

        // Turn off codegen in the base template.
        return source.Where(x => false);
    }

    void GenerateHandlers(SourceProductionContext context, ImmutableArray<TemplateArgs> args)
    {
        if (args.Length == 0)
            return;

        var model = new SelectorModel(
            args.First().StructIdNamespace,
            args.Select(x => new StructIdModel(x.StructId.ToFullName(), x.ValueType.Name)));

        var output = template.Render(model, member => member.Name);
        context.AddSource($"DapperExtensions.cs", output);
    }

    public static string Render(string @namespace, string tself, string tid)
        => template.Render(new SelectorModel(@namespace, [new StructIdModel(tself, tid)]), member => member.Name);

    record StructIdModel(string TSelf, string TId);

    record SelectorModel(string Namespace, IEnumerable<StructIdModel> Ids);
}