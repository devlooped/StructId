using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace StructId;

public class DapperGeneratorTests
{
    [Fact]
    public async Task GenerateHandler()
    {
        var test = new StructIdGeneratorTest<DapperGenerator>("UserId", "int")
        {
            SolutionTransforms =
            {
                (solution, projectId) => solution
                    .GetProject(projectId)?
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Dapper.DbString).Assembly.ManifestModule.FullyQualifiedName))
                    .Solution ?? solution
            },
            TestState =
            {
                Sources =
                {
                    """
                    using StructId;

                    public readonly partial record struct UserId(int Value): IStructId<int>;
                    """,
                },
                GeneratedSources =
                {
                    (typeof(DapperGenerator), "DapperExtensions.cs",
                    DapperGenerator.Render("StructId", "UserId", "Int32"),
                    Encoding.UTF8)
                },
            },
        }.WithAnalyzerDefaults();

        await test.RunAsync();
    }

    [Fact]
    public async Task SkipsUnsupported()
    {
        var test = new CSharpSourceGeneratorTest<DapperGenerator, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            SolutionTransforms =
            {
                (solution, projectId) => solution
                    .GetProject(projectId)?
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Dapper.DbString).Assembly.ManifestModule.FullyQualifiedName))
                    .Solution ?? solution
            },
            TestState =
            {
                Sources =
                {
                    """
                    using StructId;

                    public readonly partial record struct UserId(uint Value) : IStructId<uint>, INewable<UserId, uint>
                    {
                        public static UserId New(uint value) => new(value);
                    }
                    """,
                },
            },
        }.WithAnalyzerDefaults();

        await test.RunAsync();
    }
}
