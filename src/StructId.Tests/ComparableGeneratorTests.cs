using System.Text;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace StructId;

public class ComparableGeneratorTests
{
    [Fact]
    public async Task GenerateComparable()
    {
        var test = new CSharpSourceGeneratorTest<ComparableGenerator, DefaultVerifier>
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
                    (typeof(ComparableGenerator), "UserId.cs",
                    ThisAssembly.Resources.StructId.Templates.Comparable.Text.WithSelf("UserId", "int"),
                    Encoding.UTF8)
                },
            },
        }.WithAnalyzerStructId();

        await test.RunAsync();
    }

    [Fact]
    public async Task GenerateStringParseable()
    {
        var test = new CSharpSourceGeneratorTest<ComparableGenerator, DefaultVerifier>
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
                    (typeof(ComparableGenerator), "UserId.cs",
                    ThisAssembly.Resources.StructId.Templates.Comparable.Text.WithSelf("UserId", "int"),
                    Encoding.UTF8)
                },
            },
        }.WithAnalyzerStructId();

        await test.RunAsync();
    }
}
