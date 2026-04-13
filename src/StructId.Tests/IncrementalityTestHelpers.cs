using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace StructId;

/// <summary>
/// Helper methods for incrementality tests following the dotnet/runtime pattern.
/// </summary>
static class IncrementalityTestHelpers
{
    static readonly Lazy<ImmutableArray<MetadataReference>> cachedReferences = new(
        () => ReferenceAssemblies.Net.Net80.ResolveAsync(null, default).Result);

    /// <summary>
    /// Creates a <see cref="CSharpCompilation"/> with StructId core source files and .NET 8.0 references.
    /// </summary>
    public static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var syntaxTrees = sources.Select((s, i) =>
            CSharpSyntaxTree.ParseText(s, parseOptions, path: $"Source{i}.cs")).ToList();

        // Add StructId core sources (with paths for file-local type support)
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(ThisAssembly.Resources.StructId.IStructId.Text, parseOptions, path: "IStructId.cs"));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(ThisAssembly.Resources.StructId.IStructIdT.Text, parseOptions, path: "IStructIdT.cs"));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(ThisAssembly.Resources.StructId.INewable.Text, parseOptions, path: "INewable.cs"));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(ThisAssembly.Resources.StructId.INewableT.Text, parseOptions, path: "INewableT.cs"));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(ThisAssembly.Resources.StructId.TStructIdAttribute.Text, parseOptions, path: "TStructIdAttribute.cs"));
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(ThisAssembly.Resources.StructId.TValueAttribute.Text, parseOptions, path: "TValueAttribute.cs"));

        return CSharpCompilation.Create(
            "IncrementalityTest",
            syntaxTrees,
            cachedReferences.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Creates a <see cref="CSharpGeneratorDriver"/> with step tracking enabled.
    /// </summary>
    public static GeneratorDriver CreateDriver(params IIncrementalGenerator[] generators)
    {
        return CSharpGeneratorDriver.Create(
            generators: generators.Select(g => g.AsSourceGenerator()).ToArray(),
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
    }

    /// <summary>
    /// Asserts that all tracked output steps in the result have the expected reason.
    /// At least one of the step names must be found with outputs.
    /// </summary>
    public static void AssertAllOutputReasons(
        GeneratorRunResult result,
        IncrementalStepRunReason expectedReason,
        params string[] stepNames)
    {
        var foundAny = false;
        foreach (var stepName in stepNames)
        {
            if (!result.TrackedSteps.TryGetValue(stepName, out var steps))
                continue; // step may not have run (e.g., no matching types)

            foreach (var step in steps)
            {
                foreach (var output in step.Outputs)
                {
                    foundAny = true;
                    Assert.Equal(expectedReason, output.Reason);
                }
            }
        }

        Assert.True(foundAny,
            $"No tracked step outputs found for any of [{string.Join(", ", stepNames)}]. " +
            $"Available steps: [{string.Join(", ", result.TrackedSteps.Keys)}]");
    }

    /// <summary>
    /// Asserts that the specified tracked step outputs all have the expected reason.
    /// </summary>
    public static void AssertStepOutputReason(
        GeneratorRunResult result,
        string stepName,
        IncrementalStepRunReason expectedReason)
    {
        Assert.True(result.TrackedSteps.ContainsKey(stepName),
            $"Tracked step '{stepName}' not found. Available: {string.Join(", ", result.TrackedSteps.Keys)}");

        var steps = result.TrackedSteps[stepName];
        foreach (var step in steps)
        {
            foreach (var output in step.Outputs)
            {
                Assert.Equal(expectedReason, output.Reason);
            }
        }
    }

    /// <summary>
    /// Asserts that no step outputs contain the given reason.
    /// </summary>
    public static void AssertNoStepOutputReason(
        GeneratorRunResult result,
        string stepName,
        IncrementalStepRunReason unexpectedReason)
    {
        if (!result.TrackedSteps.TryGetValue(stepName, out var steps))
            return;

        foreach (var step in steps)
        {
            foreach (var output in step.Outputs)
            {
                Assert.NotEqual(unexpectedReason, output.Reason);
            }
        }
    }

    /// <summary>
    /// Gets the output reasons for a specific tracked step.
    /// </summary>
    public static IReadOnlyList<IncrementalStepRunReason> GetStepOutputReasons(
        GeneratorRunResult result,
        string stepName)
    {
        if (!result.TrackedSteps.TryGetValue(stepName, out var steps))
            return [];

        return steps
            .SelectMany(s => s.Outputs)
            .Select(o => o.Reason)
            .ToList();
    }

    /// <summary>
    /// Walks the object graph and asserts no <see cref="ISymbol"/> or <see cref="Compilation"/> 
    /// objects are present. Follows the dotnet/runtime 
    /// <c>SourceGenModelDoesNotEncapsulateSymbolsOrCompilationData</c> pattern.
    /// </summary>
    public static void AssertNoSymbolsOrCompilation(object? value, string path = "root")
        => AssertNoSymbolsOrCompilation(value, path, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);

    const int MaxDepth = 15;

    static void AssertNoSymbolsOrCompilation(object? value, string path, HashSet<object> visited, int depth)
    {
        if (value is null || depth > MaxDepth) return;

        Assert.False(value is ISymbol, $"Found ISymbol at {path}: {value.GetType().FullName}");
        Assert.False(value is Compilation, $"Found Compilation at {path}: {value.GetType().FullName}");
        Assert.False(value is SyntaxNode, $"Found SyntaxNode at {path}: {value.GetType().FullName}");
        Assert.False(value is SyntaxTree, $"Found SyntaxTree at {path}: {value.GetType().FullName}");

        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
            return;

        // Avoid cycles on reference types
        if (!type.IsValueType && !visited.Add(value))
            return;

        // Walk fields
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType.IsPrimitive || field.FieldType == typeof(string) || field.FieldType.IsEnum)
                continue;

            var fieldValue = field.GetValue(value);
            AssertNoSymbolsOrCompilation(fieldValue, $"{path}.{field.Name}", visited, depth + 1);
        }

        // Walk properties
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType.IsEnum)
                continue;

            if (prop.GetIndexParameters().Length > 0) continue;

            try
            {
                var propValue = prop.GetValue(value);
                AssertNoSymbolsOrCompilation(propValue, $"{path}.{prop.Name}", visited, depth + 1);
            }
            catch
            {
                // Skip properties that throw (e.g., lazy or computed)
            }
        }

        // Walk enumerable items
        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            int i = 0;
            foreach (var item in enumerable)
            {
                AssertNoSymbolsOrCompilation(item, $"{path}[{i}]", visited, depth + 1);
                i++;
            }
        }
    }
}
