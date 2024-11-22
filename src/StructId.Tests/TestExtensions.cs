using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace StructId;

public static class TestExtensions
{
    public static TTest WithCodeFixStructId<TTest>(this TTest test) where TTest : CodeFixTest<DefaultVerifier>
    {
        test.WithAnalyzerStructId();

        test.FixedState.Sources.Add(("IStructId.cs", ThisAssembly.Resources.StructId.IStructId.Text));
        test.FixedState.Sources.Add(("IStructId`1.cs", ThisAssembly.Resources.StructId.IStructId_1.Text));
        // Fixes error CS0518: Predefined type 'System.Runtime.CompilerServices.IsExternalInit' is not defined or imported
        test.FixedState.Sources.Add(
            """
            namespace System.Runtime.CompilerServices
            {
                  internal static class IsExternalInit {}
            }
            """);

        return test;
    }

    public static TTest WithAnalyzerStructId<TTest>(this TTest test) where TTest : AnalyzerTest<DefaultVerifier>
    {
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var project = solution.GetProject(projectId)!;
            var parseOptions = ((CSharpParseOptions)project.ParseOptions!).WithLanguageVersion(LanguageVersion.CSharp12);

            return project.WithParseOptions(parseOptions).Solution;
        });

        test.TestState.Sources.Add(("IStructId.cs", ThisAssembly.Resources.StructId.IStructId.Text));
        test.TestState.Sources.Add(("IStructId`1.cs", ThisAssembly.Resources.StructId.IStructId_1.Text));
        // Fixes error CS0518: Predefined type 'System.Runtime.CompilerServices.IsExternalInit' is not defined or imported
        test.TestState.Sources.Add(
            """
            namespace System.Runtime.CompilerServices
            {
                  internal static class IsExternalInit {}
            }
            """);

        return test;
    }
}
