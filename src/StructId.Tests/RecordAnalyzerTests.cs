using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using StructId;
using Xunit.Sdk;
using Test = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<StructId.RecordAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<StructId.RecordAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace StructId;

public class RecordAnalyzerTests
{
    [Fact]
    public async Task ReadonlyRecordStructNoStructId()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
            """
            public readonly record struct UserId(int Value);
            """,
        }.WithAnalyzerStructId();

        await test.RunAsync();
    }

    [Fact]
    public async Task RecordStructNoStructId()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
            """
            public record struct UserId(int Value);
            """,
        }.WithAnalyzerStructId();

        await test.RunAsync();
    }

    [Fact]
    public async Task ReadonlyStringRecordStructNotPartial()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
            """
            using StructId;

            public readonly record struct UserId : {|#0:IStructId|};
            """,
        }.WithAnalyzerStructId();

        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(Diagnostics.MustBeRecordStruct).WithLocation(0).WithArguments("UserId"));
        test.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task ReadonlyRecordStructNotPartial()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
            """
            using StructId;

            public readonly record struct UserId : {|#0:IStructId<int>|};
            """,
        }.WithAnalyzerStructId();

        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(Diagnostics.MustBeRecordStruct).WithLocation(0).WithArguments("UserId"));
        test.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task PartialRecordStructNotReadonly()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
            """
            using StructId;

            public partial record struct UserId : {|#0:IStructId<int>|};
            """,
        }.WithAnalyzerStructId();

        var expected = Verifier.Diagnostic(Diagnostics.MustBeRecordStruct).WithLocation(0).WithArguments("UserId");

        test.ExpectedDiagnostics.Add(expected);
        test.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task PartialStringRecordStructNotReadonly()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
            """
            using StructId;

            public partial record struct UserId : {|#0:IStructId|};
            """,
        }.WithAnalyzerStructId();

        var expected = Verifier.Diagnostic(Diagnostics.MustBeRecordStruct).WithLocation(0).WithArguments("UserId");

        test.ExpectedDiagnostics.Add(expected);
        test.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        await test.RunAsync();
    }


    [Fact]
    public async Task RecordStructNotReadonlyNorPartial()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
            """
            using StructId;

            public record struct UserId : {|#0:IStructId<int>|};
            """,
        }.WithAnalyzerStructId();

        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(Diagnostics.MustBeRecordStruct).WithLocation(0).WithArguments("UserId"));
        test.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task StructNotStructId()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
            """
            using StructId;

            public struct UserId : {|#0:IStructId<int>|};
            """,
        }.WithAnalyzerStructId();

        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(Diagnostics.MustBeRecordStruct).WithLocation(0).WithArguments("UserId"));
        test.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task ClassNotStructId()
    {
        var test = new Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode =
            """
            using StructId;

            public class UserId : {|#0:IStructId<int>|};
            """,
        }.WithAnalyzerStructId();

        test.ExpectedDiagnostics.Add(Verifier.Diagnostic(Diagnostics.MustBeRecordStruct).WithLocation(0).WithArguments("UserId"));
        test.ExpectedDiagnostics.Add(new DiagnosticResult("CS0535", DiagnosticSeverity.Error).WithLocation(0));

        await test.RunAsync();
    }
}
