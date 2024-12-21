using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Scriban;
using static StructId.AnalysisExtensions;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class EntityFrameworkGenerator() : BaseGenerator(
    ValueConverterType,
    ThisAssembly.Resources.Templates.EntityFramework.Text,
    ThisAssembly.Resources.Templates.EntityFramework.Text,
    ReferenceCheck.TypeExists)
{
    public const string ValueConverterType = "Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter`2";

    static readonly Dictionary<string, string> builtInTypesMap = new()
    {
        ["System.String"] = "string",
        ["System.Int32"] = "int",
        ["System.Int64"] = "long",
        ["System.Boolean"] = "bool",
        ["System.Single"] = "float",
        ["System.Double"] = "double",
        ["System.Decimal"] = "decimal",
        ["System.DateTime"] = "DateTime",
        ["System.Guid"] = "Guid",
        ["System.TimeSpan"] = "TimeSpan",
        ["System.Byte"] = "byte",
        ["System.Byte[]"] = "byte[]",
        ["System.Char"] = "char",
        ["System.UInt32"] = "uint",
        ["System.UInt64"] = "ulong",
        ["System.SByte"] = "sbyte",
        ["System.UInt16"] = "ushort",
        ["System.Int16"] = "short",
        ["System.Object"] = "object",
    };

    static readonly Template selectorTemplate = Template.Parse(ThisAssembly.Resources.EntityFrameworkSelector.Text);

    SyntaxNode? idTemplate;
    SyntaxNode? parsableIdTemplate;

    protected override IncrementalValuesProvider<TemplateArgs> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<TemplateArgs> source)
    {
        var converters = context.CompilationProvider
            .SelectMany((x, _) => x.Assembly.GetAllTypes().OfType<INamedTypeSymbol>())
            .Combine(context.CompilationProvider.Select((x, _) => x.GetTypeByMetadataName(ValueConverterType)))
            .Where(x => x.Left != null && x.Right != null &&
                x.Left.Is(x.Right) &&
                !x.Left.IsUnboundGenericType &&
                x.Left.BaseType?.TypeArguments.Length == 2 &&
                // Don't emit as plain converters if they are value templates
                !x.Left.GetAttributes().Any(a => a.IsValueTemplate()))
            .Select((x, _) => x.Left)
            .Collect();

        var templatizedValues = context.SelectTemplatizedValues()
            .Combine(context.CompilationProvider.Select((x, _) => x.GetTypeByMetadataName(ValueConverterType)))
            .Where(x => x.Left.Template.TTemplate.Is(x.Right))
            .Select((x, _) => x.Left);

        context.RegisterSourceOutput(source.Collect().Combine(converters).Combine(templatizedValues.Collect()), GenerateValueSelector);

        return base.OnInitialize(context, source);
    }

    protected override SyntaxNode SelectTemplate(TemplateArgs args)
    {
        if (args.TId.Equals(args.KnownTypes.String, SymbolEqualityComparer.Default) ||
            builtInTypesMap.ContainsKey(args.TId.ToDisplayString(NamespacedTypeName)))
            return idTemplate ??= CodeTemplate.Parse(ThisAssembly.Resources.Templates.EntityFramework.Text, args.KnownTypes.Compilation.GetParseOptions());
        else if (args.TId.Is(args.KnownTypes.Compilation.GetTypeByMetadataName("System.IParsable`1")) &&
                 args.TId.Is(args.KnownTypes.Compilation.GetTypeByMetadataName("System.IFormattable")))
            return parsableIdTemplate ??= CodeTemplate.Parse(ThisAssembly.Resources.Templates.EntityFrameworkParsable.Text, args.KnownTypes.Compilation.GetParseOptions());
        else
            return idTemplate ??= CodeTemplate.Parse(ThisAssembly.Resources.Templates.EntityFramework.Text, args.KnownTypes.Compilation.GetParseOptions());
    }

    void GenerateValueSelector(SourceProductionContext context, ((ImmutableArray<TemplateArgs>, ImmutableArray<INamedTypeSymbol>), ImmutableArray<TValueTemplate>) args)
    {
        ((var structIds, var customConverters), var templatizedConverters) = args;

        if (structIds.Length == 0 && customConverters.Length == 0 && templatizedConverters.Length == 0)
            return;

        var model = new SelectorModel(
            structIds.Select(x => new StructIdModel(x.TSelf.ToFullName(),
                // The TId is used as the ProviderClrType for EF, which should be either a built-in 
                // supported type or a parsable one. We default to using the type as-is for future-proofing, 
                // but that may be subject to change.
                !builtInTypesMap.ContainsKey(x.TId.ToDisplayString(NamespacedTypeName))
                ? x.TId.Is(x.KnownTypes.Compilation.GetTypeByMetadataName("System.IParsable`1")) &&
                  x.TId.Is(x.KnownTypes.Compilation.GetTypeByMetadataName("System.IFormattable"))
                  // parsable+formattable will result in the parsable template being used as the converter
                  // so we use string as the underlying EF type.
                  ? "string" : x.TId.ToFullName()
                : x.TId.ToFullName())),
            customConverters.Select(x => new ConverterModel(x.BaseType!.TypeArguments[0].ToFullName(), x.BaseType!.TypeArguments[1].ToFullName(), x.ToFullName())),
            templatizedConverters
                .Where(x => !builtInTypesMap.ContainsKey(x.TValue.ToDisplayString(NamespacedTypeName)))
                .Select(x => new TemplatizedModel(x)));

        var output = selectorTemplate.Render(model, member => member.Name);

        context.AddSource($"ValueConverterSelector.cs", output);
    }

    record StructIdModel(string TSelf, string TIdType)
    {
        public string TId => builtInTypesMap.TryGetValue(TIdType, out var value) ? value : TIdType;
    }

    record ConverterModel(string TModel, string TProvider, string TConverter);

    class TemplatizedModel
    {
        public TemplatizedModel(TValueTemplate template)
        {
            var declaration = template.Template.Syntax.ApplyValue(template.TValue)
               .DescendantNodes()
               .OfType<TypeDeclarationSyntax>()
               .First();

            TModel = template.TValue.ToFullName();
            TConverter = declaration.Identifier.Text;
            Code = declaration.ToFullString();
        }

        public TemplatizedModel(string tvalue, string tconverter, string code)
            => (TModel, TConverter, Code) = (tvalue, tconverter, code);

        public string TModel { get; }
        public string TConverter { get; }
        public string Code { get; }
    }

    record SelectorModel(IEnumerable<StructIdModel> Ids, IEnumerable<ConverterModel> Converters, IEnumerable<TemplatizedModel> Templatized);
}