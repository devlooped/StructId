using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using StructId;
using Xunit.Sdk;
using Test = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<StructId.TemplateAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<StructId.TemplateAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace StructId;

public class TemplateAnalyzerTests
{
    [Fact]
    public async Task RecordStructNoTemplate()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                public record struct TSelf(int Value);
                """,
        }.WithAnalyzerDefaults();

        await test.RunAsync();
    }

    [Fact]
    public async Task ClassNoTemplate()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                public class TSelf(int Value);
                """,
        }.WithAnalyzerDefaults();

        await test.RunAsync();
    }

    [Fact]
    public async Task ClassRecordNoTemplate()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                public record TSelf(int Value);
                """,
        }.WithAnalyzerDefaults();

        await test.RunAsync();
    }

    [Fact]
    public async Task TemplateRecordStructNotPartial()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;

                [TStructId]
                file record struct {|#0:TSelf|};
                """,
        }.WithAnalyzerDefaults();

        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(Diagnostics.TemplateMustBeFileRecordStruct).WithLocation(0).WithArguments("TSelf"));

        await test.RunAsync();
    }

    [Fact]
    public async Task TemplateRecordStructNotFile()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;

                [TStructId]
                partial record struct {|#0:TSelf|};
                """,
        }.WithAnalyzerDefaults();

        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(Diagnostics.TemplateMustBeFileRecordStruct).WithLocation(0).WithArguments("TSelf"));

        await test.RunAsync();
    }

    [Fact]
    public async Task TemplateRecordClassNotStruct()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;

                [TStructId]
                file partial record {|#0:TSelf|};
                """,
        }.WithAnalyzerDefaults();

        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(Diagnostics.TemplateMustBeFileRecordStruct).WithLocation(0).WithArguments("TSelf"));

        await test.RunAsync();
    }

    [Fact]
    public async Task TemplateStructNotRecord()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;

                [TStructId]
                file partial struct {|#0:TSelf|};
                """,
        }.WithAnalyzerDefaults();

        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(Diagnostics.TemplateMustBeFileRecordStruct).WithLocation(0).WithArguments("TSelf"));

        await test.RunAsync();
    }

    [Fact]
    public async Task TemplateWithNonValueConstructor()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;
            
                [TStructId]
                file partial record struct TSelf(int {|#0:value|});
                """,
        }.WithAnalyzerDefaults();

        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(Diagnostics.TemplateConstructorValueConstructor).WithLocation(0).WithArguments("TSelf"));

        await test.RunAsync();
    }

    [Fact]
    public async Task TemplateNotTSelf()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;

                [TStructId]
                file partial record struct {|#0:Foo|};
                """,
        }.WithAnalyzerDefaults();

        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(Diagnostics.TemplateDeclarationNotTSelf).WithLocation(0).WithArguments("Foo"));

        await test.RunAsync();
    }

    [Fact]
    public async Task PartialTSelfNotFileLocal()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;

                [TStructId]
                file partial record struct TSelf;

                partial record struct {|#0:TSelf|};
                """,
        }.WithAnalyzerDefaults();

        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(Diagnostics.TemplateMustBeFileRecordStruct).WithLocation(0).WithArguments("TSelf"));

        await test.RunAsync();
    }
}
