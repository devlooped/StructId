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
        var code = DapperGenerator.RenderCustom("StructId", "UserId", "System.Ulid", "StringUlidHandler");

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
                    (typeof(DapperGenerator), "DapperExtensions.cs", code, Encoding.UTF8)
                },
            },
        }.WithAnalyzerDefaults();

        await test.RunAsync();
    }

    [Fact]
    public async Task GenerateTempletizedHandler()
    {
        var code = DapperGenerator.RenderTemplatized("StructId", "UserId", "System.Ulid", "System_Ulid_TypeHandler",
            """
            file class System_Ulid_TypeHandler : Dapper.SqlMapper.TypeHandler<System.Ulid>
            {
                public override System.Ulid Parse(object value) => System.Ulid.Parse((string)value, null);
            
                public override void SetValue(IDbDataParameter parameter, System.Ulid value)
                {
                    parameter.DbType = DbType.String;
                    parameter.Value = value.ToString(null, null);
                }
            }
            """);

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
                    using System.Diagnostics.CodeAnalysis;
                    using StructId;

                    [TValue]
                    file class TValue_TypeHandler : Dapper.SqlMapper.TypeHandler<TValue>
                    {
                        public override TValue Parse(object value) => TValue.Parse((string)value, null);

                        public override void SetValue(IDbDataParameter parameter, TValue value)
                        {
                            parameter.DbType = DbType.String;
                            parameter.Value = value.ToString(null, null);
                        }
                    }

                    file partial struct TValue : IParsable<TValue>, IFormattable
                    {
                        public static TValue Parse(string s, IFormatProvider? provider) => throw new NotImplementedException();
                        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TValue result) => throw new NotImplementedException();
                        public string ToString(string? format, IFormatProvider? formatProvider) => throw new NotImplementedException();
                    }
                    """
                },
                GeneratedSources =
                {
                    (typeof(DapperGenerator), "DapperExtensions.cs", code, Encoding.UTF8)
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
