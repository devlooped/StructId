using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Scriban;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class DapperGenerator() : BaseGenerator(
    "Dapper.SqlMapper+TypeHandler`1", "", "", ReferenceCheck.TypeExists)
{
    static readonly Template template = Template.Parse(ThisAssembly.Resources.DapperExtensions.Text);

    static string GetBuiltInHandlerName(StructIdModel model) => model.ValueTypeFullName switch
    {
        "System.String" or "string" => "String",
        "System.Int32" or "int" => "Int32",
        "System.Int64" or "long" => "Int64",
        "System.Guid" or "Guid" => "Guid",
        _ => model.ValueTypeName,
    };

    protected override IncrementalValuesProvider<StructIdModel> OnInitialize(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<StructIdModel> source)
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

        var builtInHandled = source.Where(x => IsBuiltIn(x.ValueTypeFullName))
            .WithTrackingName(TrackingNames.BuiltInHandled);

        // Any type in the compilation that inherits from Dapper.SqlMapper.TypeHandler<T> is also picked up,
        // unless its a value template. Extract as (HandlerFullName, HandledValueTypeFullName) pairs.
        var customHandlers = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax cds &&
                    cds.BaseList?.Types.Any(t => t.Type.ToString().Contains("TypeHandler")) == true,
                transform: static (ctx, ct) =>
                {
                    if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is not INamedTypeSymbol symbol)
                        return default((string, string)?);

                    var typeHandlerType = ctx.SemanticModel.Compilation.GetTypeByMetadataName("Dapper.SqlMapper+TypeHandler`1");
                    if (typeHandlerType == null || !symbol.Is(typeHandlerType))
                        return null;

                    if (symbol.GetAttributes().Any(a => a.IsValueTemplate()) ||
                        symbol.ContainingType?.IsStructIdTemplate() == true)
                        return null;

                    // Find the T in TypeHandler<T> by walking the base type
                    var baseType = symbol.BaseType;
                    while (baseType != null)
                    {
                        if (baseType.OriginalDefinition.Equals(typeHandlerType, SymbolEqualityComparer.Default) &&
                            baseType.TypeArguments.Length == 1)
                        {
                            return ((string, string)?)(symbol.ToFullName(), baseType.TypeArguments[0].ToFullName());
                        }
                        baseType = baseType.BaseType;
                    }
                    return null;
                })
            .Where(static x => x != null)
            .Select(static (x, _) => x!.Value)
            .Collect()
            .WithTrackingName(TrackingNames.CustomHandlers);

        // Non built-in value types can be templatized by using [TValue] templates
        var templatizedValues = context.SelectTemplatizedValues()
            .Where(x => !IsBuiltIn(x.ValueTypeFullName))
            .Where(static x => x.IsSubtypeOf("Dapper.SqlMapper.TypeHandler<TValue>"))
            .WithTrackingName(TrackingNames.TemplatizedValues);

        // Match struct IDs to custom handlers or templatized handlers
        var customHandled = source
            .Combine(customHandlers.Combine(templatizedValues.Collect()))
            .Select(static (x, _) =>
            {
                var (model, (handlers, templatized)) = x;

                // Try to find a direct custom handler for this value type
                var handler = handlers.FirstOrDefault(h => h.Item2 == model.ValueTypeFullName);
                if (handler.Item1 != null)
                    return (Model: model, HandlerName: handler.Item1);

                // Try templatized handlers
                var templated = templatized.FirstOrDefault(t => t.ValueTypeFullName == model.ValueTypeFullName);
                if (templated.AppliedTypeName != null)
                    return (Model: model, HandlerName: templated.AppliedTypeName);

                return default;
            })
            .Where(static x => x.HandlerName != null);

        context.RegisterSourceOutput(builtInHandled.Collect().Combine(customHandled.Collect()).Combine(templatizedValues.Collect()), GenerateHandlers);

        // Turn off codegen in the base template.
        return source.Where(x => false);
    }

    void GenerateHandlers(SourceProductionContext context, ((ImmutableArray<StructIdModel> builtInHandled, ImmutableArray<(StructIdModel Model, string HandlerName)> customHandled), ImmutableArray<TemplatizedValueOutput> templatizedValues) source)
    {
        var ((builtInHandled, customHandled), templatizedValues) = source;
        if (builtInHandled.Length == 0 && customHandled.Length == 0 && templatizedValues.Length == 0)
            return;

        var structIdNamespace = builtInHandled.Select(x => x.CoreNamespace).Concat(customHandled.Select(x => x.Model.CoreNamespace)).FirstOrDefault()
            ?? "StructId";

        var templatizedHandlers = new HashSet<string>(templatizedValues
            .Select(x => x.AppliedTypeName));

        var customValueHandlers = customHandled
            .GroupBy(x => x.HandlerName)
            // Avoid registering twice the same templatized value handlers since they are
            // already added at the end of the scriban rendering.
            .Where(x => !templatizedHandlers.Contains(x.Key))
            .Select(x => new ValueHandlerModel(x.First().Model.ValueTypeFullName, x.Key))
            .ToArray();

        var model = new SelectorModel(
            structIdNamespace,
            // Built-in use the Name of the value type since it's used as a suffix for well-known provided implementations.
            builtInHandled.Select(x => new DapperStructIdModel(x.TypeFullName, GetBuiltInHandlerName(x))),
            customHandled.Select(x => new StructIdCustomModel(x.Model.TypeFullName, x.Model.ValueTypeFullName, x.HandlerName)),
            customValueHandlers,
            templatizedValues.Select(x => new ValueHandlerModelCode(x.ValueTypeFullName, x.AppliedTypeName, x.AppliedCode)));

        var output = template.Render(model, member => member.Name);
        context.AddSource($"DapperExtensions.cs", output.Trim());
    }

    public static string Render(string @namespace, string tself, string tvalue)
        => template.Render(new SelectorModel(@namespace, [new(tself, tvalue)], [], [], []), member => member.Name).Trim();

    public static string RenderCustom(string @namespace, string tself, string tvalue, string thandler)
        => template.Render(new SelectorModel(@namespace, [], [new(tself, tvalue, thandler)], [new(tvalue, thandler)], []), member => member.Name).Trim();

    public static string RenderTemplatized(string @namespace, string tself, string tvalue, string thandler, string handlerCode)
        => template.Render(new SelectorModel(@namespace, [], [new(tself, tvalue, thandler)], [], [new(tvalue, thandler, handlerCode)]), member => member.Name).Trim();

    record DapperStructIdModel(string TSelf, string TValue);

    record StructIdCustomModel(string TSelf, string TValue, string THandler);

    record ValueHandlerModel(string TValue, string THandler);

    record ValueHandlerModelCode(string TValue, string THandler, string Code);

    record SelectorModel(
        string Namespace,
        IEnumerable<DapperStructIdModel> Ids,
        IEnumerable<StructIdCustomModel> CustomIds,
        IEnumerable<ValueHandlerModel> CustomValues,
        IEnumerable<ValueHandlerModelCode> TemplatizedValueHandlers);
}
