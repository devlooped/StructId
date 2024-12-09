using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace StructId;

public class ConstructorGeneratorTests
{
    [Fact]
    public async Task GenerateRecordConstructor()
    {
        var test = new CSharpSourceGeneratorTest<ConstructorGenerator, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources =
                {
                    """
                    using StructId;
                    namespace Foo;

                    public readonly partial record struct UserId : IStructId<int>;
                    """,
                },
            },
        }.WithAnalyzerDefaults();

        await test.RunAsync();
    }

    [Fact]
    public async Task GenerateRecordStringConstructor()
    {
        var test = new CSharpSourceGeneratorTest<ConstructorGenerator, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources =
                {
                    """
                    using StructId;
                    namespace Foo;

                    public readonly partial record struct UserId : IStructId;
                    """,
                },
            },
        }.WithAnalyzerDefaults();

        await test.RunAsync();
    }

    [Fact]
    public async Task GenerateRecordConstructorInGlobalNamespace()
    {
        var test = new CSharpSourceGeneratorTest<ConstructorGenerator, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources =
                {
                    """
                    using StructId;

                    public readonly partial record struct UserId : IStructId<int>;
                    """,
                },
            },
        }.WithAnalyzerDefaults();

        await test.RunAsync();
    }

}
