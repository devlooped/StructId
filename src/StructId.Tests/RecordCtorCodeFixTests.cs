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

public class RecordCtorCodeFixTests
{
    [Fact]
    public async Task RenameValue()
    {
        var test = new CSharpCodeFixTest<RecordAnalyzer, RenameCtorCodeFix, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
            """
            using StructId;
            
            public readonly partial record struct UserId(int {|#0:foo|}) : {|#1:IStructId<int>|};
            """,
            FixedCode =
            """
            using StructId;
            
            public readonly partial record struct UserId(int Value) : IStructId<int>;
            """,
        }.WithCodeFixStructId();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(Diagnostics.MustHaveValueConstructor).WithLocation(0).WithArguments("UserId"));
        test.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(1));

        // Don't propagate the expected diagnostics to the fixed code, it will have none of them
        test.FixedState.InheritanceMode = StateInheritanceMode.Explicit;

        await test.RunAsync();
    }

    [Fact]
    public async Task RemoveCtor()
    {
        var test = new CSharpCodeFixTest<RecordAnalyzer, RemoveCtorCodeFix, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
            """
            using StructId;
            
            public readonly partial record struct UserId(int {|#0:foo|}) : {|#1:IStructId<int>|};
            """,
            FixedCode =
            """
            using StructId;
            
            public readonly partial record struct UserId: {|#0:IStructId<int>|};
            """,
        }.WithCodeFixStructId();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(Diagnostics.MustHaveValueConstructor).WithLocation(0).WithArguments("UserId"));
        test.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(1));

        // Don't propagate the expected diagnostics to the fixed code, it will have none of them
        test.FixedState.InheritanceMode = StateInheritanceMode.Explicit;
        test.FixedState.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        await test.RunAsync();
    }

}
