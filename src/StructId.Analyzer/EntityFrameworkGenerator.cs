using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Scriban;

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

    // Combined set of both keys (System.Int32) and values (int) for efficient lookup
    static readonly HashSet<string> builtInEFTypes = new(builtInTypesMap.Keys.Concat(builtInTypesMap.Values));

    static readonly Template selectorTemplate = Template.Parse(ThisAssembly.Resources.EntityFrameworkSelector.Text);

    protected override IncrementalValuesProvider<StructIdModel> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<StructIdModel> source)
    {
        // Discover custom ValueConverter<TModel,TProvider> types via syntax predicate
        var converters = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax cds &&
                    cds.BaseList?.Types.Any(t => t.Type.ToString().Contains("ValueConverter")) == true,
                transform: static (ctx, ct) =>
                {
                    if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is not INamedTypeSymbol symbol || symbol.IsUnboundGenericType)
                        return default((string, string, string)?);

                    var converterType = ctx.SemanticModel.Compilation.GetTypeByMetadataName(ValueConverterType);
                    if (converterType == null || !symbol.Is(converterType))
                        return null;

                    if (symbol.GetAttributes().Any(a => a.IsValueTemplate()) ||
                        symbol.ContainingType?.IsStructIdTemplate() == true)
                        return null;

                    if (symbol.BaseType?.TypeArguments.Length != 2)
                        return null;

                    return ((string, string, string)?)(
                        symbol.BaseType.TypeArguments[0].ToFullName(),
                        symbol.BaseType.TypeArguments[1].ToFullName(),
                        symbol.ToFullName());
                })
            .Where(static x => x != null)
            .Select(static (x, _) => x!.Value)
            .Collect()
            .WithTrackingName(TrackingNames.Converters);

        // Templatized value converters from [TValue] templates
        var templatizedValues = context.SelectTemplatizedValues()
            .Where(static x => x.IsSubtypeOf("Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<TValue,TValue>"));

        context.RegisterSourceOutput(source.Collect().Combine(converters).Combine(templatizedValues.Collect()), GenerateValueSelector);

        return base.OnInitialize(context, source);
    }

    protected override string SelectTemplate(StructIdModel model)
    {
        if (builtInEFTypes.Contains(model.ValueTypeFullName))
            return ThisAssembly.Resources.Templates.EntityFramework.Text;

        if (UsesStringProvider(model))
            return ThisAssembly.Resources.Templates.EntityFrameworkParsable.Text;

        return ThisAssembly.Resources.Templates.EntityFramework.Text;
    }

    void GenerateValueSelector(SourceProductionContext context, ((ImmutableArray<StructIdModel>, ImmutableArray<(string TModel, string TProvider, string TConverter)>), ImmutableArray<TemplatizedValueOutput>) args)
    {
        ((var structIds, var customConverters), var templatizedConverters) = args;

        if (structIds.Length == 0 && customConverters.Length == 0 && templatizedConverters.Length == 0)
            return;

        var model = new SelectorModel(
            structIds.Select(x => new EFStructIdModel(x.TypeFullName,
                !builtInEFTypes.Contains(x.ValueTypeFullName)
                ? UsesStringProvider(x)
                  ? "string" : x.ValueTypeFullName
                : x.ValueTypeFullName)),
            customConverters.Select(x => new ConverterModel(x.TModel, x.TProvider, x.TConverter)),
            templatizedConverters
                .Where(x => !builtInEFTypes.Contains(x.ValueTypeFullName))
                .Select(x => new TemplatizedModel(x.ValueTypeFullName, x.AppliedTypeName, x.AppliedCode)));

        var output = selectorTemplate.Render(model, member => member.Name);

        context.AddSource($"ValueConverterSelector.cs", output);
    }

    record EFStructIdModel(string TSelf, string TValueType)
    {
        public string TValue => builtInTypesMap.TryGetValue(TValueType, out var value) ? value : TValueType;
    }

    static bool UsesStringProvider(StructIdModel model) =>
        HasInterface(model, "System.IParsable") &&
        HasInterface(model, "System.IFormattable");

    static bool HasInterface(StructIdModel model, string interfaceType) =>
        model.ValueTypeAllInterfaces.AsImmutableArray().Any(i => StripGenericArgs(i) == interfaceType);

    static string StripGenericArgs(string name)
    {
        var idx = name.IndexOf('<');
        return idx >= 0 ? name.Substring(0, idx) : name;
    }

    record ConverterModel(string TModel, string TProvider, string TConverter);

    record TemplatizedModel(string TModel, string TConverter, string Code);

    record SelectorModel(IEnumerable<EFStructIdModel> Ids, IEnumerable<ConverterModel> Converters, IEnumerable<TemplatizedModel> Templatized);
}
