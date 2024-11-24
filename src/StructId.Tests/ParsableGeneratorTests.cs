using System.Text;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using static StructId.RoslynTestingExtensions;

namespace StructId;

public class ParsableGeneratorTests
{
    [Fact]
    public async Task GenerateParseable()
    {
        var test = new CSharpSourceGeneratorTest<ParsableGenerator, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestState =
            {
                Sources =
                {
                    """
                    using StructId;

                    public readonly partial record struct UserId(int Value) : IStructId<int>;
                    """,
                },
                GeneratedSources =                 
                {
                    (typeof(ParsableGenerator), "UserId.cs", 
                    ThisAssembly.Resources.StructId.Templates.TParsable.Text.Replace("TStruct", "UserId").Replace("TValue", "int"),
                    Encoding.UTF8)
                },
            },
        }.WithAnalyzerStructId();

        await test.RunAsync();
    }

    [Fact]
    public async Task GenerateStringParseable()
    {
        var test = new CSharpSourceGeneratorTest<ParsableGenerator, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestState =
            {
                Sources =
                {
                    """
                    using StructId;

                    public readonly partial record struct UserId(string Value) : IStructId;
                    """,
                },
                GeneratedSources =
                {
                    (typeof(ParsableGenerator), "UserId.cs",
                    ThisAssembly.Resources.StructId.Templates.SParsable.Text.Replace("SStruct", "UserId"),
                    Encoding.UTF8)
                },
            },
        }.WithAnalyzerStructId();

        await test.RunAsync();
    }

    [Fact]
    public async Task NoParseableAvailable()
    {
        var test = new CSharpSourceGeneratorTest<ParsableGenerator, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            TestState =
            {
                Sources =
                {
                    """
                    using StructId;

                    public readonly partial record struct UserId(int Value) : IStructId<int>;
                    """,
                },
            },
        }.WithAnalyzerStructId();

        await test.RunAsync();
    }

}
