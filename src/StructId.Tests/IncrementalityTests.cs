using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static StructId.IncrementalityTestHelpers;

namespace StructId;

/// <summary>
/// Tests verifying the incremental behavior of StructId source generators.
/// Following the patterns established by dotnet/runtime's System.Text.Json and LibraryImport generators.
/// </summary>
public class IncrementalityTests
{
    const string GuidStructId = """
        using StructId;
        public readonly partial record struct UserId : IStructId<System.Guid>;
        """;

    const string IntStructId = """
        using StructId;
        public readonly partial record struct OrderId : IStructId<int>;
        """;

    const string StringStructId = """
        using StructId;
        public readonly partial record struct TagId : IStructId;
        """;

    const string UnrelatedClass = """
        public class UnrelatedService { public int DoWork() => 42; }
        """;

    #region ConstructorGenerator (representative BaseGenerator subclass)

    [Fact]
    public void ConstructorGenerator_SameInput_AllCached()
    {
        var compilation = CreateCompilation(GuidStructId);
        var generator = new ConstructorGenerator();
        var driver = CreateDriver(generator);

        // Run 1: all New
        driver = driver.RunGenerators(compilation);
        var result1 = driver.GetRunResult().Results[0];
        AssertAllOutputReasons(result1, IncrementalStepRunReason.New,
            TrackingNames.ReferenceType,
            TrackingNames.StructIds, TrackingNames.Combined);

        // Run 2: same input, should be all Cached
        driver = driver.RunGenerators(compilation);
        var result2 = driver.GetRunResult().Results[0];
        AssertAllOutputReasons(result2, IncrementalStepRunReason.Cached,
            TrackingNames.ReferenceType,
            TrackingNames.StructIds, TrackingNames.Combined);
    }

    [Fact]
    public void ConstructorGenerator_UnrelatedChange_OutputsCached()
    {
        var compilation = CreateCompilation(GuidStructId);
        var generator = new ConstructorGenerator();
        var driver = CreateDriver(generator);

        // Run 1
        driver = driver.RunGenerators(compilation);

        // Add unrelated class
        compilation = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(UnrelatedClass,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest)));

        // Run 2: existing outputs should ideally be Cached
        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult().Results[0];

        // After fix, Combined step should be Cached for existing outputs
        AssertStepOutputReason(result, TrackingNames.Combined, IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void ConstructorGenerator_NewStructId_ProducesAdditionalOutput()
    {
        var compilation = CreateCompilation(GuidStructId);
        var generator = new ConstructorGenerator();
        var driver = CreateDriver(generator);

        // Run 1
        driver = driver.RunGenerators(compilation);
        var result1 = driver.GetRunResult();
        var outputCount1 = result1.GeneratedTrees.Length;

        // Add a second StructId
        compilation = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(IntStructId,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest)));

        // Run 2: should have more outputs
        driver = driver.RunGenerators(compilation);
        var result2 = driver.GetRunResult();
        Assert.True(result2.GeneratedTrees.Length > outputCount1,
            $"Expected more outputs after adding StructId. Before: {outputCount1}, After: {result2.GeneratedTrees.Length}");
    }

    [Fact]
    public void ConstructorGenerator_RemovedStructId_OutputRemoved()
    {
        var compilation = CreateCompilation(GuidStructId, IntStructId);
        var generator = new ConstructorGenerator();
        var driver = CreateDriver(generator);

        // Run 1: both StructIds produce output
        driver = driver.RunGenerators(compilation);
        var result1 = driver.GetRunResult();
        var outputCount1 = result1.GeneratedTrees.Length;
        Assert.True(outputCount1 > 0);

        // Remove one StructId by replacing compilation without it
        var treeToRemove = compilation.SyntaxTrees.First(t =>
            t.GetText().ToString().Contains("OrderId"));
        compilation = compilation.RemoveSyntaxTrees(treeToRemove);

        // Run 2: should have fewer outputs
        driver = driver.RunGenerators(compilation);
        var result2 = driver.GetRunResult();
        Assert.True(result2.GeneratedTrees.Length < outputCount1,
            $"Expected fewer outputs after removing StructId. Before: {outputCount1}, After: {result2.GeneratedTrees.Length}");
    }

    [Fact]
    public void ConstructorGenerator_StringStructId_SameInput_AllCached()
    {
        var compilation = CreateCompilation(StringStructId);
        var generator = new ConstructorGenerator();
        var driver = CreateDriver(generator);

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult().Results[0];

        AssertAllOutputReasons(result, IncrementalStepRunReason.Cached,
            TrackingNames.ReferenceType,
            TrackingNames.StructIds, TrackingNames.Combined);
    }

    #endregion

    #region SystemTextJsonGenerator

    [Fact]
    public void SystemTextJsonGenerator_SameInput_AllCached()
    {
        // SystemTextJsonGenerator requires IParsable<T> which Guid implements
        var compilation = CreateCompilation(GuidStructId);
        var generator = new SystemTextJsonGenerator();
        var driver = CreateDriver(generator);

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult().Results[0];

        AssertAllOutputReasons(result, IncrementalStepRunReason.Cached,
            TrackingNames.ReferenceType,
            TrackingNames.StructIds, TrackingNames.Combined);
    }

    #endregion

    #region TemplatedGenerator

    [Fact]
    public void TemplatedGenerator_SameInput_AllCached()
    {
        // Use string-based struct id with Parsable (string) template
        var templateSource = ThisAssembly.Resources.StructId.Templates.Parsable.Text;
        var compilation = CreateCompilation(StringStructId, templateSource);
        var generator = new TemplatedGenerator();
        var driver = CreateDriver(generator);

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult().Results[0];

        AssertAllOutputReasons(result, IncrementalStepRunReason.Cached,
            TrackingNames.Templates,
            TrackingNames.TemplatizedStructIds);
    }

    [Fact]
    public void TemplatedGenerator_UnrelatedChange_OutputsCached()
    {
        var templateSource = ThisAssembly.Resources.StructId.Templates.Parsable.Text;
        var compilation = CreateCompilation(StringStructId, templateSource);
        var generator = new TemplatedGenerator();
        var driver = CreateDriver(generator);

        driver = driver.RunGenerators(compilation);

        compilation = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(UnrelatedClass,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path: "Unrelated.cs"));

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult().Results[0];

        // After fix, TemplatizedStructIds step should be Cached for existing outputs
        AssertStepOutputReason(result, TrackingNames.TemplatizedStructIds, IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void TemplatedGenerator_NewStructId_ProducesAdditionalOutput()
    {
        // Use ParsableT template which matches typed struct ids (e.g. Guid implements IParsable)
        var templateSource = ThisAssembly.Resources.StructId.Templates.ParsableT.Text;
        var compilation = CreateCompilation(GuidStructId, templateSource);
        var generator = new TemplatedGenerator();
        var driver = CreateDriver(generator);

        driver = driver.RunGenerators(compilation);
        var outputCount1 = driver.GetRunResult().GeneratedTrees.Length;

        compilation = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText(IntStructId,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path: "OrderId.cs"));

        driver = driver.RunGenerators(compilation);
        var outputCount2 = driver.GetRunResult().GeneratedTrees.Length;

        Assert.True(outputCount2 > outputCount1,
            $"Expected more outputs. Before: {outputCount1}, After: {outputCount2}");
    }

    #endregion

    #region Pipeline Model Inspection

    [Fact]
    public void ConstructorGenerator_PipelineModels_NoSymbolsOrCompilation()
    {
        var compilation = CreateCompilation(GuidStructId);
        var generator = new ConstructorGenerator();
        var driver = CreateDriver(generator);

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult().Results[0];

        // Check Combined step outputs (TemplateArgs)
        if (result.TrackedSteps.TryGetValue(TrackingNames.Combined, out var steps))
        {
            foreach (var step in steps)
            {
                foreach (var output in step.Outputs)
                {
                    AssertNoSymbolsOrCompilation(output.Value);
                }
            }
        }
    }

    [Fact]
    public void TemplatedGenerator_PipelineModels_NoSymbolsOrCompilation()
    {
        var templateSource = ThisAssembly.Resources.StructId.Templates.Parsable.Text;
        var compilation = CreateCompilation(StringStructId, templateSource);
        var generator = new TemplatedGenerator();
        var driver = CreateDriver(generator);

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult().Results[0];

        foreach (var stepName in new[] { TrackingNames.Templates, TrackingNames.TemplatizedStructIds })
        {
            if (result.TrackedSteps.TryGetValue(stepName, out var steps))
            {
                foreach (var step in steps)
                {
                    foreach (var output in step.Outputs)
                    {
                        AssertNoSymbolsOrCompilation(output.Value);
                    }
                }
            }
        }
    }

    #endregion

    #region Equivalent Source Changes

    [Fact]
    public void ConstructorGenerator_WhitespaceChange_OutputUnchangedOrCached()
    {
        var compilation = CreateCompilation(GuidStructId);
        var generator = new ConstructorGenerator();
        var driver = CreateDriver(generator);

        driver = driver.RunGenerators(compilation);

        // Add whitespace to the StructId source (semantically equivalent)
        var tree = compilation.SyntaxTrees.First(t =>
            t.GetText().ToString().Contains("UserId"));
        var newTree = CSharpSyntaxTree.ParseText(
            GuidStructId + "\n\n// comment\n",
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        compilation = compilation.ReplaceSyntaxTree(tree, newTree);

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult().Results[0];

        // After fix, the Combined step should show Unchanged (semantically equivalent input)
        var reasons = GetStepOutputReasons(result, TrackingNames.Combined);
        Assert.NotEmpty(reasons);
        Assert.All(reasons, r => Assert.True(
            r == IncrementalStepRunReason.Unchanged || r == IncrementalStepRunReason.Cached,
            $"Expected Unchanged or Cached, got {r}"));
    }

    #endregion

    #region Reference Removal

    [Fact]
    public void ConstructorGenerator_NoStructIds_NoOutput()
    {
        // Compilation with no StructId types
        var compilation = CreateCompilation("public class Foo {}");
        var generator = new ConstructorGenerator();
        var driver = CreateDriver(generator);

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();

        // Should produce no generated sources from this generator
        Assert.Empty(result.Results[0].GeneratedSources);
    }

    #endregion

    #region Multiple Generators

    [Fact]
    public void MultipleGenerators_SameInput_AllCached()
    {
        var compilation = CreateCompilation(GuidStructId);
        var driver = CreateDriver(
            new ConstructorGenerator(),
            new SystemTextJsonGenerator());

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation);
        var results = driver.GetRunResult().Results;

        foreach (var result in results)
        {
            AssertAllOutputReasons(result, IncrementalStepRunReason.Cached,
                TrackingNames.ReferenceType,
                TrackingNames.StructIds, TrackingNames.Combined);
        }
    }

    #endregion
}
