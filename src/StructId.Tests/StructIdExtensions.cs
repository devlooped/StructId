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

public static partial class StructIdExtensions
{
    public static string WithSelf(this string template, string typeName, string? valueType = default)
        => template.Replace(SelfExpr(), typeName).Replace(TSelfExpr(), typeName).Replace(TValueExpr(), valueType ?? "string");

    public static TTest WithCodeFixDefaults<TTest>(this TTest test) where TTest : CodeFixTest<DefaultVerifier>
    {
        test.WithAnalyzerDefaults();

        AddSourceIfNotExists(test.FixedState.Sources, "IStructId.cs", ThisAssembly.Resources.StructId.IStructId.Text);
        AddSourceIfNotExists(test.FixedState.Sources, "IStructIdT.cs", ThisAssembly.Resources.StructId.IStructIdT.Text);
        AddSourceIfNotExists(test.FixedState.Sources, "INewable.cs", ThisAssembly.Resources.StructId.INewable.Text);
        AddSourceIfNotExists(test.FixedState.Sources, "INewableT.cs", ThisAssembly.Resources.StructId.INewableT.Text);
        AddSourceIfNotExists(test.FixedState.Sources, "TStructIdAttribute.cs", ThisAssembly.Resources.StructId.TStructIdAttribute.Text);

        return test;
    }

    public static TTest WithAnalyzerDefaults<TTest>(this TTest test) where TTest : AnalyzerTest<DefaultVerifier>
    {
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var project = solution.GetProject(projectId)!;
            var parseOptions = ((CSharpParseOptions)project.ParseOptions!).WithLanguageVersion(LanguageVersion.CSharp12);

            return project.WithParseOptions(parseOptions).Solution;
        });

        AddSourceIfNotExists(test.TestState.Sources, "IStructId.cs", ThisAssembly.Resources.StructId.IStructId.Text);
        AddSourceIfNotExists(test.TestState.Sources, "IStructIdT.cs", ThisAssembly.Resources.StructId.IStructIdT.Text);
        AddSourceIfNotExists(test.TestState.Sources, "INewable.cs", ThisAssembly.Resources.StructId.INewable.Text);
        AddSourceIfNotExists(test.TestState.Sources, "INewableT.cs", ThisAssembly.Resources.StructId.INewableT.Text);
        AddSourceIfNotExists(test.TestState.Sources, "TStructIdAttribute.cs", ThisAssembly.Resources.StructId.TStructIdAttribute.Text);

        return test;
    }

    static void AddSourceIfNotExists(SourceFileList sources, string filename, string content)
    {
        if (!sources.Any(s => s.filename == filename))
        {
            sources.Add((filename, content));
        }
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
