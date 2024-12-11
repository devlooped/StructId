using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace StructId;

public class CodeTemplateTests(ITestOutputHelper output)
{
    static CSharpParseOptions parse = new CSharpParseOptions(LanguageVersion.Latest);
    // An empty C# compilation
    Compilation compilation = CSharpCompilation.Create("Test",
        syntaxTrees:
        [
            CSharpSyntaxTree.ParseText(ThisAssembly.Resources.StructId.IStructId.Text, parse),
            CSharpSyntaxTree.ParseText(ThisAssembly.Resources.StructId.IStructIdT.Text, parse),
            CSharpSyntaxTree.ParseText(ThisAssembly.Resources.StructId.TStructIdAttribute.Text, parse),
        ],
        options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
        references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

    [Fact]
    public void AddsStructIdNamespace()
    {
        var template =
            """
            using StructId;
            
            [TStructId]
            file partial record struct TSelf(string Value)
            {
                // from template
            }

            file record struct TId;
            """;

        var id = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
            """
            using StructId;

            namespace Foo;

            public partial record struct UserId : IStructId<long>;
            """, parse)).GetTypeByMetadataName("Foo.UserId");

        Assert.NotNull(id);

        var applied = CodeTemplate.Parse(template).Apply(id).NormalizeWhitespace().ToFullString().Trim();

        Assert.Equal(
            CodeTemplate.Parse(
            """
            using StructId;

            namespace Foo;

            partial record struct UserId
            {
                // from template
            }
            """).NormalizeWhitespace().ToFullString().Trim().ReplaceLineEndings(),
            applied.ReplaceLineEndings());
    }

    [Fact]
    public void PreservesPrimaryConstructor()
    {
        var template =
            """
            using StructId;
            
            [TStructId]
            file partial record struct TSelf(/*🙏*/ string Value);
            """;

        var id = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
            """
            using StructId;

            namespace Foo;

            public partial record struct UserId : IStructId;
            """, parse)).GetTypeByMetadataName("Foo.UserId");

        Assert.NotNull(id);

        var applied = CodeTemplate.Parse(template).Apply(id).NormalizeWhitespace().ToFullString().Trim();

        Assert.Equal(
            CodeTemplate.Parse(
            """
            using StructId;

            namespace Foo;

            partial record struct UserId(string Value);
            """).NormalizeWhitespace().ToFullString().Trim().ReplaceLineEndings(),
            applied.ReplaceLineEndings());
    }

    [Fact]
    public void RemovesFileLocalTypes()
    {
        var template =
            """
            using StructId;
            
            [TStructId]
            file partial record struct TSelf
            {
              // From template
            }

            file record TSome;
            file class TAnother;
            file record struct TYetAnother;
            """;

        var applied = CodeTemplate.Apply(template, "Foo", "string", normalizeWhitespace: true);

        Assert.Equal(
            CodeTemplate.Parse(
            """
            using StructId;

            partial record struct Foo
            {
              // From template
            }            
            """).NormalizeWhitespace().ToFullString().Trim().ReplaceLineEndings(),
            applied.ReplaceLineEndings());
    }

    [Fact]
    public void PreservesTrivia()
    {
        var template =
            """
            using System;

            // Test
            [TStructId]
            file partial record struct TSelf(Ulid Value)
            {
                public static TSelf New() => new(Ulid.NewUlid());
            }
            """;

        var applied = CodeTemplate.Apply(template, "ItemId", "Ulid");

        Assert.Equal(
            """
            using System;
            
            // Test
            partial record struct ItemId
            {
                public static ItemId New() => new(Ulid.NewUlid());
            }
            """,
            applied);
    }

    [Fact]
    public void AppliesValueTemplate()
    {
        var template =
            """
            using System;

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
            }
            """;

        var applied = CodeTemplate.Apply(template, "System.Ulid");

        Assert.Equal(
            """
            using System;
            file class System_Ulid_TypeHandler : Dapper.SqlMapper.TypeHandler<System.Ulid>
            {
                public override System.Ulid Parse(object value) => System.Ulid.Parse((string)value, null);
            
                public override void SetValue(IDbDataParameter parameter, System.Ulid value)
                {
                    parameter.DbType = DbType.String;
                    parameter.Value = value.ToString(null, null);
                }
            }
            """,
            applied);
    }
}
