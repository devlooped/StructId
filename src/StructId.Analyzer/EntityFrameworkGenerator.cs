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
        var converters = context.CompilationProvider
            .SelectMany((x, _) => x.Assembly.GetAllTypes().OfType<INamedTypeSymbol>())
            .Combine(context.CompilationProvider.Select((x, _) => x.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter`2")))
            .Where(x => x.Left != null && x.Right != null &&
                x.Left.Is(x.Right) &&
                !x.Left.IsUnboundGenericType &&
                x.Left.BaseType?.TypeArguments.Length == 2 &&
                // Don't emit as plain converters if they are id templates
                !x.Left.GetAttributes().Any(a => a.IsValueTemplate()))
            .Select((x, _) => x.Left)
            .Collect();

        context.RegisterSourceOutput(source.Collect().Combine(converters), GenerateValueSelector);

        return base.OnInitialize(context, source);
    }

    void GenerateValueSelector(SourceProductionContext context, (ImmutableArray<TemplateArgs>, ImmutableArray<INamedTypeSymbol>) args)
    {
        (var ids, var converters) = args;

        if (ids.Length == 0)
            return;

        var model = new SelectorModel(
            ids.Select(x => new StructIdModel(x.TSelf.ToFullName(), x.TId.ToFullName())),
            converters.Select(x => new ConverterModel(x.BaseType!.TypeArguments[0].ToFullName(), x.BaseType!.TypeArguments[1].ToFullName(), x.ToFullName())));

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

    record ConverterModel(string TModel, string TProvider, string TConverter);

    record SelectorModel(IEnumerable<StructIdModel> Ids, IEnumerable<ConverterModel> Converters);
}