using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Scriban;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class DapperGenerator() : BaseGenerator(
    "Dapper.SqlMapper+TypeHandler`1", "", "", ReferenceCheck.TypeExists)
{
    static readonly Template template = Template.Parse(ThisAssembly.Resources.DapperExtensions.Text);

    protected override IncrementalValuesProvider<TemplateArgs> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<TemplateArgs> source)
    {
        bool IsBuiltIn(string type) => type switch
        {
            "System.String" => true,
            "System.Guid" => true,
            "System.Int32" => true,
            "System.Int64" => true,
            "string" => true,
            "int" => true,
            "long" => true,
            _ => false
        };

        var builtInHandled = source.Where(x => IsBuiltIn(x.TValue.ToFullName()));

        var customHandlers = context.CompilationProvider
            .SelectMany((x, _) => x.Assembly.GetAllTypes().OfType<INamedTypeSymbol>())
            .Combine(context.CompilationProvider.Select((x, _) => x.GetTypeByMetadataName("Dapper.SqlMapper+TypeHandler`1")))
            .Where(x => x.Left != null && x.Right != null &&
                x.Left.Is(x.Right) &&
                // Don't emit as plain handlers if they are id templates
                !x.Left.GetAttributes().Any(a => a.IsValueTemplate()))
            .Select((x, _) => x.Left)
            .Collect();

        var templatizedValues = context.SelectTemplatizedValues()
            .Where(x => !IsBuiltIn(x.TValue.ToFullName()))
            .Combine(context.CompilationProvider.Select((x, _) => x.GetTypeByMetadataName("Dapper.SqlMapper+TypeHandler`1")))
            .Where(x => x.Left.Template.TTemplate.Is(x.Right))
            .Select((x, _) => x.Left);

        var customHandled = source
            .Combine(customHandlers.Combine(templatizedValues.Collect()))
            .Select((x, _) =>
            {
                (TemplateArgs args, (ImmutableArray<INamedTypeSymbol> handlers, ImmutableArray<TValueTemplate> templatized)) = x;

                var handlerType = args.ReferenceType.Construct(args.TValue);
                var handler = handlers.FirstOrDefault(x => x.Is(handlerType, false));

                if (handler == null)
                {
                    var templated = templatized.Where(x => x.TValue.Equals(args.TValue, SymbolEqualityComparer.Default))
                        .FirstOrDefault();
                    // Consider templatized handlers that will be emitted as custom handlers too for registration.
                    if (templated != null)
                    {
                        var compilation = args.KnownTypes.Compilation.AddSyntaxTrees(templated.Syntax.SyntaxTree);
                        handler = compilation.Assembly.GetAllTypes().FirstOrDefault(x => x.Name == templated.TypeName);
                    }
                }

                return args with { ReferenceType = handler! };
            })
            .Where(x => x.ReferenceType != null);

        context.RegisterSourceOutput(builtInHandled.Collect().Combine(customHandled.Collect()).Combine(templatizedValues.Collect()), GenerateHandlers);

        // Turn off codegen in the base template.
        return source.Where(x => false);
    }

    void GenerateHandlers(SourceProductionContext context, ((ImmutableArray<TemplateArgs> builtInHandled, ImmutableArray<TemplateArgs> customHandled), ImmutableArray<TValueTemplate> templatizedValues) source)
    {
        var ((builtInHandled, customHandled), templatizedValues) = source;
        if (builtInHandled.Length == 0 && customHandled.Length == 0 && templatizedValues.Length == 0)
            return;

        var structIdNamespace = builtInHandled.Concat(customHandled).Select(x => x.KnownTypes.StructIdNamespace).FirstOrDefault()
            ?? "StructId";

        var templatizedHandlers = new HashSet<string>(templatizedValues
            .Select(x => x.TypeName));

        var customValueHandlers = customHandled
            .GroupBy(x => x.ReferenceType.ToFullName())
            // Avoid registering twice the same templatized value handlers since they are 
            // already added at the end of the scriban rendering.
            .Where(x => !templatizedHandlers.Contains(x.Key))
            .Select(x => new ValueHandlerModel(x.First().TValue.ToFullName(), x.Key))
            .ToArray();

        var model = new SelectorModel(
            structIdNamespace,
            // Built-in use the Name of the value type since it's used as a suffix for well-known provided implementations.
            builtInHandled.Select(x => new StructIdModel(x.TSelf.ToFullName(), x.TValue.Name)),
            customHandled.Select(x => new StructIdCustomModel(x.TSelf.ToFullName(), x.TValue.ToFullName(), x.ReferenceType.ToFullName())),
            customValueHandlers,
            templatizedValues.Select(x => new ValueHandlerModelCode(x)));

        var output = template.Render(model, member => member.Name);
        context.AddSource($"DapperExtensions.cs", output.Trim());
    }

    public static string Render(string @namespace, string tself, string tvalue)
        => template.Render(new SelectorModel(@namespace, [new(tself, tvalue)], [], [], []), member => member.Name).Trim();

    public static string RenderCustom(string @namespace, string tself, string tvalue, string thandler)
        => template.Render(new SelectorModel(@namespace, [], [new(tself, tvalue, thandler)], [new(tvalue, thandler)], []), member => member.Name).Trim();

    public static string RenderTemplatized(string @namespace, string tself, string tvalue, string thandler, string handlerCode)
        => template.Render(new SelectorModel(@namespace, [], [new(tself, tvalue, thandler)], [], [new(tvalue, thandler, handlerCode)]), member => member.Name).Trim();

    record StructIdModel(string TSelf, string TValue);

    record StructIdCustomModel(string TSelf, string TValue, string THandler);

    record ValueHandlerModel(string TValue, string THandler);

    class ValueHandlerModelCode
    {
        public ValueHandlerModelCode(TValueTemplate template)
        {
            var declaration = template.Template.Syntax.ApplyValue(template.TValue)
               .DescendantNodes()
               .OfType<TypeDeclarationSyntax>()
               .First();

            TValue = template.TValue.ToFullName();
            THandler = declaration.Identifier.Text;
            Code = declaration.ToFullString();
        }

        public ValueHandlerModelCode(string tvalue, string thandler, string code)
            => (TValue, THandler, Code) = (tvalue, thandler, code);

        public string TValue { get; }
        public string THandler { get; }
        public string Code { get; }
    }

    record SelectorModel(
        string Namespace,
        IEnumerable<StructIdModel> Ids,
        IEnumerable<StructIdCustomModel> CustomIds,
        IEnumerable<ValueHandlerModel> CustomValues,
        IEnumerable<ValueHandlerModelCode> TemplatizedValueHandlers);
}