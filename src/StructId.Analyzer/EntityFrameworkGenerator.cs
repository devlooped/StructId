using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Scriban;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class EntityFrameworkGenerator() : BaseGenerator(
    "Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter`2",
    ThisAssembly.Resources.Templates.EntityFramework.Text,
    ThisAssembly.Resources.Templates.EntityFramework.Text,
    ReferenceCheck.TypeExists)
{
    static readonly Template template = Template.Parse(ThisAssembly.Resources.EntityFrameworkSelector.Text);

    protected override IncrementalValuesProvider<TemplateArgs> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<TemplateArgs> source)
    {
        context.RegisterSourceOutput(source.Collect(), GenerateValueSelector);

        return base.OnInitialize(context, source);
    }

    void GenerateValueSelector(SourceProductionContext context, ImmutableArray<TemplateArgs> args)
    {
        if (args.Length == 0)
            return;

        var model = new SelectorModel(args.Select(x => new StructIdModel(x.StructId.ToFullName(), x.ValueType.ToFullName())));
        var output = template.Render(model, member => member.Name);
        context.AddSource($"ValueConverterSelector.cs", output);
    }

    record StructIdModel(string TSelf, string TIdType)
    {
        public string TId => TIdType switch
        {
            "System.String" => "string",
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.Boolean" => "bool",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Decimal" => "decimal",
            "System.DateTime" => "DateTime",
            "System.Guid" => "Guid",
            "System.TimeSpan" => "TimeSpan",
            "System.Byte" => "byte",
            "System.Byte[]" => "byte[]",
            "System.Char" => "char",
            "System.UInt32" => "uint",
            "System.UInt64" => "ulong",
            "System.SByte" => "sbyte",
            "System.UInt16" => "ushort",
            "System.Int16" => "short",
            "System.Object" => "object",
            _ => TIdType
        };
    }

    record SelectorModel(IEnumerable<StructIdModel> Ids);
}