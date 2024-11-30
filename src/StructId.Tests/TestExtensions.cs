using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace StructId;

public static partial class TestExtensions
{
    public static string WithSelf(this string template, string typeName, string? valueType = default)
        => template.Replace(SelfExpr(), typeName).Replace(TSelfExpr(), typeName).Replace(TValueExpr(), valueType ?? "string");

    public static TTest WithCodeFixStructId<TTest>(this TTest test) where TTest : CodeFixTest<DefaultVerifier>
    {
        test.WithAnalyzerStructId();

        test.FixedState.Sources.Add(("IStructId.cs", ThisAssembly.Resources.StructId.IStructId.Text));
        test.FixedState.Sources.Add(("IStructIdT.cs", ThisAssembly.Resources.StructId.IStructIdT.Text));
        test.FixedState.Sources.Add(("INewable.cs", ThisAssembly.Resources.StructId.INewable.Text));
        test.FixedState.Sources.Add(("INewableT.cs", ThisAssembly.Resources.StructId.INewableT.Text));
        test.FixedState.Sources.Add(("TStructIdAttribute.cs", ThisAssembly.Resources.StructId.TStructIdAttribute.Text));

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
        test.TestState.Sources.Add(("IStructIdT.cs", ThisAssembly.Resources.StructId.IStructIdT.Text));
        test.TestState.Sources.Add(("INewable.cs", ThisAssembly.Resources.StructId.INewable.Text));
        test.TestState.Sources.Add(("INewableT.cs", ThisAssembly.Resources.StructId.INewableT.Text));
        test.TestState.Sources.Add(("TStructIdAttribute.cs", ThisAssembly.Resources.StructId.TStructIdAttribute.Text));

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

    public static string Replace(this string template, Regex regex, string value)
        => regex.Replace(template, value);

    [GeneratedRegex(@"\bSelf\b")]
    private static partial Regex SelfExpr();

    [GeneratedRegex(@"\bTSelf\b")]
    private static partial Regex TSelfExpr();

    [GeneratedRegex(@"\bTId\b")]
    private static partial Regex TValueExpr();
}
