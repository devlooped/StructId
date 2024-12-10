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
        var supported = source.Where(x => x.TId.ToFullName() switch
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

        var handlers = context.CompilationProvider
            .SelectMany((x, _) => x.Assembly.GetAllTypes().OfType<INamedTypeSymbol>())
            .Combine(context.CompilationProvider.Select((x, _) => x.GetTypeByMetadataName("Dapper.SqlMapper+TypeHandler`1")))
            .Where(x => x.Left != null && x.Right != null && x.Left.Is(x.Right))
            .Select((x, _) => x.Left)
            .Collect();

        var custom = source
            .Combine(handlers)
            .Select((x, _) =>
            {
                (TemplateArgs args, ImmutableArray<INamedTypeSymbol> handlers) = x;

                var handlerType = args.ReferenceType.Construct(args.TId);
                var handler = handlers.FirstOrDefault(x => x.Is(handlerType, false));

                return args with { ReferenceType = handler! };
            })
            .Where(x => x.ReferenceType != null);

        context.RegisterSourceOutput(supported.Collect().Combine(custom.Collect()), GenerateHandlers);

        // Turn off codegen in the base template.
        return source.Where(x => false);
    }

    void GenerateHandlers(SourceProductionContext context, (ImmutableArray<TemplateArgs> ids, ImmutableArray<TemplateArgs> custom) source)
    {
        var (ids, custom) = source;
        if (ids.Length == 0 && custom.Length == 0)
            return;

        var known = ids.Concat(custom).First().KnownTypes;
        var customHandlers = custom.Select(x => x.ReferenceType.ToFullName()).Distinct().ToArray();

        var model = new SelectorModel(
            known.StructIdNamespace,
            ids.Select(x => new StructIdModel(x.TSelf.ToFullName(), x.TId.Name)),
            custom.Select(x => new StructIdCustomModel(x.TSelf.ToFullName(), x.TId.Name, x.ReferenceType.ToFullName())),
            customHandlers);

        var output = template.Render(model, member => member.Name);
        context.AddSource($"DapperExtensions.cs", output);
    }

    public static string Render(string @namespace, string tself, string tid)
        => template.Render(new SelectorModel(@namespace, [new(tself, tid)], [], []), member => member.Name);

    public static string RenderCustom(string @namespace, string tself, string tid, string thandler)
        => template.Render(new SelectorModel(@namespace, [], [new(tself, tid, thandler)], [thandler]), member => member.Name);

    record StructIdModel(string TSelf, string TId);

    record StructIdCustomModel(string TSelf, string TId, string THandler);

    record SelectorModel(string Namespace, IEnumerable<StructIdModel> Ids, IEnumerable<StructIdCustomModel> CustomIds, IEnumerable<string> CustomHandlers);
}