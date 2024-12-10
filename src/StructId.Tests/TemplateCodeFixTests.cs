using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using static StructId.Diagnostics;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<StructId.TemplateAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace StructId;

public class TemplateCodeFixTests
{
    [Fact]
    public async Task AddPartialKeyword()
    {
        var test = new CSharpCodeFixTest<TemplateAnalyzer, TemplateCodeFix, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;
                
                [TStructId]
                file record struct {|#0:TSelf|};
                """,
            FixedCode =
                """
                using StructId;
            
                [TStructId]
                file partial record struct TSelf;
                """,
        }.WithCodeFixDefaults();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(TemplateMustBeFileRecordStruct).WithLocation(0).WithArguments("TSelf"));

        await test.RunAsync();
    }

    [Fact]
    public async Task RemovePublicKeyword()
    {
        var test = new CSharpCodeFixTest<TemplateAnalyzer, TemplateCodeFix, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;
                
                [TStructId]
                public partial record struct {|#0:TSelf|};
                """,
            FixedCode =
                """
                using StructId;
            
                [TStructId]
                file partial record struct TSelf;
                """,
        }.WithCodeFixDefaults();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(TemplateMustBeFileRecordStruct).WithLocation(0).WithArguments("TSelf"));

        await test.RunAsync();
    }

    [Fact]
    public async Task RemoveInternalKeyword()
    {
        var test = new CSharpCodeFixTest<TemplateAnalyzer, TemplateCodeFix, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;
                
                [TStructId]
                internal partial record struct {|#0:TSelf|};
                """,
            FixedCode =
                """
                using StructId;
            
                [TStructId]
                file partial record struct TSelf;
                """,
        }.WithCodeFixDefaults();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(TemplateMustBeFileRecordStruct).WithLocation(0).WithArguments("TSelf"));

        await test.RunAsync();
    }

    [Fact]
    public async Task ChangeStructToRecordStruct()
    {
        var test = new CSharpCodeFixTest<TemplateAnalyzer, TemplateCodeFix, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;
            
                [TStructId]
                file partial struct {|#0:TSelf|};
                """,
            FixedCode =
                """
                using StructId;
            
                [TStructId]
                file partial record struct TSelf;
                """,
        }.WithCodeFixDefaults();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(TemplateMustBeFileRecordStruct).WithLocation(0).WithArguments("TSelf"));

        await test.RunAsync();
    }

    [Fact]
    public async Task ChangeClassToRecordStruct()
    {
        var test = new CSharpCodeFixTest<TemplateAnalyzer, TemplateCodeFix, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;
            
                [TStructId]
                file class {|#0:TSelf|};
                """,
            FixedCode =
                """
                using StructId;
            
                [TStructId]
                file partial record struct TSelf;
                """,
        }.WithCodeFixDefaults();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(TemplateMustBeFileRecordStruct).WithLocation(0).WithArguments("TSelf"));

        await test.RunAsync();
    }

    [Fact]
    public async Task RenameTSelf()
    {
        var test = new CSharpCodeFixTest<TemplateAnalyzer, TemplateCodeFix, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;
            
                [TStructId]
                file partial record struct {|#0:Foo|};
                """,
            FixedCode =
                """
                using StructId;
            
                [TStructId]
                file partial record struct TSelf;
                """,
        }.WithCodeFixDefaults();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(TemplateDeclarationNotTSelf).WithLocation(0).WithArguments("Foo"));

        await test.RunAsync();
    }

    [Fact]
    public async Task AddFileLocal()
    {
        var test = new CSharpCodeFixTest<TemplateAnalyzer, TemplateCodeFix, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;
            
                [TStructId]
                partial record struct {|#0:TSelf|};
                """,
            FixedCode =
                """
                using StructId;
            
                [TStructId]
                file partial record struct TSelf;
                """,
        }.WithCodeFixDefaults();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(TemplateMustBeFileRecordStruct).WithLocation(0).WithArguments("TSelf"));

        await test.RunAsync();
    }

    [Fact]
    public async Task AddFileLocalToPartial()
    {
        var test = new CSharpCodeFixTest<TemplateAnalyzer, TemplateCodeFix, DefaultVerifier>
        {
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipLocalDiagnosticCheck,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
                """
                using StructId;
            
                [TStructId]
                file partial record struct TSelf;

                partial record struct {|#0:TSelf|};

                partial record struct {|#1:TSelf|};
                """,
            FixedCode =
                """
                using StructId;
            
                [TStructId]
                file partial record struct TSelf;

                file partial record struct TSelf;

                file partial record struct TSelf;
                """,
        }.WithCodeFixDefaults();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(TemplateMustBeFileRecordStruct).WithLocation(0).WithArguments("TSelf"));
        test.ExpectedDiagnostics.Add(new DiagnosticResult(TemplateMustBeFileRecordStruct).WithLocation(1).WithArguments("TSelf"));

        await test.RunAsync();
    }
}
