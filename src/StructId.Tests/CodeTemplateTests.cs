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
}
