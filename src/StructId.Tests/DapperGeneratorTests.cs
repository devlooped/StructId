using System.Text;
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
        }.WithAnalyzerDefaults()
            .WithReferencePackages(new PackageIdentity("Dapper", "2.1.35"));

        await test.RunAsync();
    }

    [Fact]
    public async Task GenerateStringHandler()
    {
        var test = new StructIdGeneratorTest<DapperGenerator>("UserId", "string")
        {
            TestState =
            {
                Sources =
                {
                    """
                    using StructId;

                    public readonly partial record struct UserId(string Value): IStructId;
                    """,
                },
                GeneratedSources =
                {
                    (typeof(DapperGenerator), "DapperExtensions.cs",
                    DapperGenerator.Render("StructId", "UserId", "String"),
                    Encoding.UTF8)
                },
            },
        }.WithAnalyzerDefaults()
            .WithReferencePackages(new PackageIdentity("Dapper", "2.1.35"));

        await test.RunAsync();
    }

    [Fact]
    public async Task GenerateCustomHandler()
    {
        var code = DapperGenerator.RenderCustom("StructId", "UserId", "System.Ulid", "StringUlidHandler");

        var test = new StructIdGeneratorTest<DapperGenerator>("UserId", "System.Ulid")
        {
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
        }.WithAnalyzerDefaults()
            .WithReferencePackages(
                new PackageIdentity("Dapper", "2.1.72"),
                new PackageIdentity("Ulid", "1.4.1"));

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
            TestState =
            {
                Sources =
                {
                    """
                    using System;
                    using StructId;

                    public readonly partial record struct UserId(Ulid Value): IStructId<Ulid>;
                    """,
                    ThisAssembly.Resources.StructId.Templates.DapperTypeHandler.Text
                },
                GeneratedSources =
                {
                    (typeof(DapperGenerator), "DapperExtensions.cs", code, Encoding.UTF8)
                },
            },
        }.WithAnalyzerDefaults()
            .WithReferencePackages(
                new PackageIdentity("Dapper", "2.1.72"),
                new PackageIdentity("Ulid", "1.4.1"));

        await test.RunAsync();
    }

    [Fact]
    public async Task SkipsUnsupported()
    {
        var test = new CSharpSourceGeneratorTest<DapperGenerator, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
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
        }.WithAnalyzerDefaults()
            .WithReferencePackages(new PackageIdentity("Dapper", "2.1.35"));

        await test.RunAsync();
    }
}
