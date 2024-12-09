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
        var test = new StructIdGeneratorTest<NewtonsoftJsonGenerator>("UserId", "int")
        {
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
        };

        await test.RunAsync();
    }

    [Fact]
    public async Task GenerateIfNewtonsoftJsonPresent()
    {
        var test = new StructIdGeneratorTest<NewtonsoftJsonGenerator>("UserId", "int")
        {
            SolutionTransforms =
            {
                (solution, projectId) => solution
                    .GetProject(projectId)?
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(JsonConvert).Assembly.Location))
                    .Solution ?? solution
            },
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
                    CodeTemplate.Apply(ThisAssembly.Resources.StructId.Templates.NewtonsoftJsonConverterT.Text, "UserId", "int"),
                    Encoding.UTF8),
                    (typeof(NewtonsoftJsonGenerator), "NewtonsoftJsonConverter.cs",
                    ThisAssembly.Resources.StructId.Templates.NewtonsoftJsonConverter_1.Text,
                    Encoding.UTF8)
                },
            },
        };

        await test.RunAsync();
    }
}
