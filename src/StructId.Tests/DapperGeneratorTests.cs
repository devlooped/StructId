using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace StructId;

public class DapperGeneratorTests(ITestOutputHelper output)
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
    public async Task GenerateCustomHandler()
    {
        output.WriteLine(DapperGenerator.RenderCustom("StructId", "UserId", "Ulid", "StringUlidHandler"));

        var test = new StructIdGeneratorTest<DapperGenerator>("UserId", "System.Ulid")
        {
            SolutionTransforms =
            {
                (solution, projectId) => solution
                    .GetProject(projectId)?
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Dapper.DbString).Assembly.Location))
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Ulid).Assembly.Location))
                    .Solution ?? solution
            },
            TestState =
            {
                Sources =
                {
                    """
                    using System;
                    using StructId;

                    public readonly partial record struct UserId(Ulid Value): IStructId<Ulid>;
                    """,
                    """
                    using System;
                    using System.Data;
                    using Dapper;
                    
                    public class StringUlidHandler : Dapper.SqlMapper.TypeHandler<Ulid>
                    {
                        public override Ulid Parse(object value)
                        {
                            return Ulid.Parse((string)value);
                        }

                        public override void SetValue(IDbDataParameter parameter, Ulid value)
                        {
                            parameter.DbType = DbType.StringFixedLength;
                            parameter.Size = 26;
                            parameter.Value = value.ToString();
                        }
                    }
                    """
                },
                GeneratedSources =
                {
                    (typeof(DapperGenerator), "DapperExtensions.cs",
                    DapperGenerator.RenderCustom("StructId", "UserId", "Ulid", "StringUlidHandler"),
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
