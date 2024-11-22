using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<StructId.RecordAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace StructId;

public class RecordCodeFixTests
{
    [Fact]
    public async Task AddPartialKeyword()
    {
        var test = new CSharpCodeFixTest<RecordAnalyzer, RecordCodeFix, DefaultVerifier>
        {
            TestCode =
            """
            using StructId;
            
            public readonly record struct UserId : {|#0:IStructId<int>|};
            """,
            FixedCode =
            """
            using StructId;
            
            public readonly partial record struct UserId : {|#0:IStructId<int>|};
            """,
        }.WithCodeFixStructId();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(Diagnostics.MustBeRecordStruct).WithLocation(0).WithArguments("UserId"));
        test.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        // Don't propagate the expected diagnostics to the fixed code, it will have none of them
        test.FixedState.InheritanceMode = StateInheritanceMode.Explicit;
        test.FixedState.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task AddReadOnlyModifier()
    {
        var test = new CSharpCodeFixTest<RecordAnalyzer, RecordCodeFix, DefaultVerifier>
        {
            TestCode =
            """
            using StructId;
            
            public partial record struct UserId : {|#0:IStructId<int>|};
            """,
            FixedCode =
            """
            using StructId;
            
            public readonly partial record struct UserId : {|#0:IStructId<int>|};
            """,
        }.WithCodeFixStructId();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(Diagnostics.MustBeRecordStruct).WithLocation(0).WithArguments("UserId"));
        test.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        // Don't propagate the expected diagnostics to the fixed code, it will have none of them
        test.FixedState.InheritanceMode = StateInheritanceMode.Explicit;
        test.FixedState.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task ChangeRecordStruct()
    {
        var test = new CSharpCodeFixTest<RecordAnalyzer, RecordCodeFix, DefaultVerifier>
        {
            TestCode =
            """
            using StructId;
            
            public partial record UserId : {|#0:IStructId<int>|};
            """,
            FixedCode =
            """
            using StructId;
            
            public readonly partial record struct UserId : {|#0:IStructId<int>|};
            """,
        }.WithCodeFixStructId();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(Diagnostics.MustBeRecordStruct).WithLocation(0).WithArguments("UserId"));
        test.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        // Don't propagate the expected diagnostics to the fixed code, it will have none of them
        test.FixedState.InheritanceMode = StateInheritanceMode.Explicit;
        test.FixedState.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        await test.RunAsync();
    }
}
