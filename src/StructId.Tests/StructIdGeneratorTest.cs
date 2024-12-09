using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace StructId;

class StructIdGeneratorTest<TGenerator> : CSharpSourceGeneratorTest<TGenerator, DefaultVerifier>
    where TGenerator : new()
{
    readonly Type[] additionalGenerators;

    public StructIdGeneratorTest(string structIdType, string valueType, params Type[] additionalGenerators)
    {
        this.additionalGenerators = additionalGenerators;
        this.WithAnalyzerDefaults();

        ReferenceAssemblies = ReferenceAssemblies.Net.Net80;

        TestState.Sources.Add(("Newable.cs", ThisAssembly.Resources.StructId.Templates.Newable.Text));
        TestState.Sources.Add(("NewableGuid.cs", ThisAssembly.Resources.StructId.Templates.NewableGuid.Text));
        TestState.Sources.Add(("NewableT.cs", ThisAssembly.Resources.StructId.Templates.NewableT.Text));
        TestState.Sources.Add(("Parsable.cs", ThisAssembly.Resources.StructId.Templates.Parsable.Text));
        TestState.Sources.Add(("ParsableT.cs", ThisAssembly.Resources.StructId.Templates.ParsableT.Text));

        var hintPath = valueType switch
        {
            "Guid" => "NewableGuid.cs",
            "string" => "Newable.cs",
            _ => "NewableT.cs",
        };

        var template = valueType switch
        {
            "Guid" => ThisAssembly.Resources.StructId.Templates.NewableGuid.Text,
            "string" => ThisAssembly.Resources.StructId.Templates.Newable.Text,
            _ => ThisAssembly.Resources.StructId.Templates.NewableT.Text,
        };

        TestState.GeneratedSources.Add(
            (typeof(TemplatedGenerator), $"{structIdType}/{hintPath}",
            CodeTemplate.Apply(template, structIdType, valueType),
            Encoding.UTF8));

        TestState.GeneratedSources.Add(
            (typeof(TemplatedGenerator), $"{structIdType}/Parsable{(valueType == "string" ? "" : "T")}.cs",
            CodeTemplate.Apply(valueType == "string" ?
                ThisAssembly.Resources.StructId.Templates.Parsable.Text :
                ThisAssembly.Resources.StructId.Templates.ParsableT.Text,
                structIdType, valueType),
            Encoding.UTF8));
    }

    protected override IEnumerable<Type> GetSourceGenerators()
    {
        if (typeof(TGenerator) != typeof(TemplatedGenerator))
            yield return typeof(TemplatedGenerator);

        if (typeof(TGenerator) != typeof(ConstructorGenerator))
            yield return typeof(ConstructorGenerator);

        yield return typeof(TGenerator);

        foreach (var generator in additionalGenerators)
        {
            yield return generator;
        }
    }
}
