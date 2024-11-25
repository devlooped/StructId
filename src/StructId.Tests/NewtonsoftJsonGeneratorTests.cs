using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit.Sdk;

namespace StructId;

public class NewtonsoftJsonGeneratorTests
{
    [Fact]
    public async Task DoesNotGenerateIfNewtonsoftJsonNotPresent()
    {
        var test = new CSharpSourceGeneratorTest<NewtonsoftJsonGenerator, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestState =
            {
                Sources =
                {
                    """
                    using StructId;

                    public readonly partial record struct UserId(int Value) : IStructId<int>;
                    """,
                },
            },
        }.WithAnalyzerStructId();

        await test.RunAsync();
    }

    [Fact]
    public async Task GenerateIfNewtonsoftJsonPresent()
    {
        var test = new StructIdSourceGeneratorTest<NewtonsoftJsonGenerator>("int")
        {
            SolutionTransforms =
            {
                (solution, projectId) => solution
                    .GetProject(projectId)?
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(JsonConvert).Assembly.Location))
                    .Solution ?? solution
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestState =
            {
                Sources =
                {
                    """
                    using StructId;

                    public readonly partial record struct UserId(int Value) : IStructId<int>;
                    """,
                },
                GeneratedSources =
                {
                    (typeof(NewtonsoftJsonGenerator), "UserId.cs",
                    ThisAssembly.Resources.StructId.Templates.NewtonsoftJsonConverterT.Text.Replace("TSelf", "UserId").Replace("TValue", "int"),
                    Encoding.UTF8),
                    (typeof(NewtonsoftJsonGenerator), "NewtonsoftJsonConverter.cs",
                    ThisAssembly.Resources.StructId.Templates.NewtonsoftJsonConverter_1.Text,
                    Encoding.UTF8)
                },
            },
        }.WithAnalyzerStructId();

        await test.RunAsync();
    }

    class StructIdSourceGeneratorTest<TGenerator> : CSharpSourceGeneratorTest<TGenerator, DefaultVerifier>
        where TGenerator : new()
    {
        public StructIdSourceGeneratorTest(string? tvalue = null)
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80;

            if (tvalue != null)
            {
                TestState.GeneratedSources.Add(
                    (typeof(NewableGenerator), "UserId.cs",
                    ThisAssembly.Resources.StructId.Templates.NewableT.Text.Replace("TSelf", "UserId").Replace("TValue", tvalue),
                    Encoding.UTF8));
                TestState.GeneratedSources.Add(
                    (typeof(ParsableGenerator), "UserId.cs",
                    ThisAssembly.Resources.StructId.Templates.ParsableT.Text.Replace("TSelf", "UserId").Replace("TValue", "int"),
                    Encoding.UTF8));
            }
            else
            {
                TestState.GeneratedSources.Add(
                    (typeof(NewableGenerator), "UserId.cs",
                    ThisAssembly.Resources.StructId.Templates.Newable.Text.Replace("Self", "UserId"),
                    Encoding.UTF8));
                TestState.GeneratedSources.Add(
                    (typeof(ParsableGenerator), "UserId.cs",
                    ThisAssembly.Resources.StructId.Templates.Parsable.Text.Replace("Self", "UserId"),
                    Encoding.UTF8));
            }
        }

        protected override IEnumerable<Type> GetSourceGenerators()
        {
            yield return typeof(NewableGenerator);
            yield return typeof(ParsableGenerator);
            yield return typeof(TGenerator);
        }
    }
}
