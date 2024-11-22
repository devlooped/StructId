﻿using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class ConstructorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var ids = context.CompilationProvider
            .SelectMany((x, _) => x.Assembly.GetAllTypes().OfType<INamedTypeSymbol>())
            .Where(t => t.IsStructId())
            .Where(t => t.IsPartial());

        context.RegisterSourceOutput(ids, GenerateCode);
    }

    void GenerateCode(SourceProductionContext context, INamedTypeSymbol symbol)
    {
        var ns = symbol.ContainingNamespace.Equals(symbol.ContainingModule.GlobalNamespace, SymbolEqualityComparer.Default) 
            ? null 
            : symbol.ContainingNamespace.ToDisplayString();

        // Generic IStructId<T> -> T, otherwise string
        var type = symbol.AllInterfaces.First(x => x.Name == "IStructId").TypeArguments.Select(x => x.GetTypeName(ns)).FirstOrDefault() ?? "string";

        var kind = symbol.IsRecord && symbol.IsValueType ?
            "record struct" :
            symbol.IsRecord ?
            "record" :
            "class";

        var output = new StringBuilder();

        output.AppendLine("// <auto-generated/>");
        if (ns != null)
            output.AppendLine($"namespace {ns};");

        output.AppendLine(
            $$"""

            [System.CodeDom.Compiler.GeneratedCode("StructId", "{{ThisAssembly.Info.InformationalVersion}}")]
            partial {{kind}} {{symbol.Name}}({{type}} Value);
            """);

        context.AddSource($"{symbol.ToFileName()}.ctor.cs", output.ToString());
    }
}
