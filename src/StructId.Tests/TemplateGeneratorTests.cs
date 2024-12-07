using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace StructId;

public class TemplateGeneratorTests
{
    public readonly partial record struct UserId(int Value) : IStructId<int>;

    public record struct Id(string value) : IComparable<Id>
    {
        int IComparable<Id>.CompareTo(Id other) => value.CompareTo(other.value);
    }

    [Fact]
    public async Task GenerateComparable()
    {
        var test = new GeneratorTest<TemplatedGenerator>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources =
                {
                    """
                    using StructId;

                    public readonly partial record struct UserId(int Value) : IStructId<int>;
                    """,
                    """
                    using System;
                    using StructId;

                    [TStructId]
                    file partial record struct TSelf(TId Value) : IComparable<TSelf>
                    {
                        public int CompareTo(TSelf other) => Value.CompareTo(other.Value);
                    }

                    file record struct TId : IComparable<TId>
                    {
                        public int CompareTo(TId other) => throw new NotImplementedException();
                    }
                    """
                },
            },
        }.WithAnalyzerStructId();

        await test.RunAsync();

        // get compilation after generation run
        var compilation = test.Compilation;

        Assert.NotNull(compilation);
        Assert.NotNull(compilation.GetTypeByMetadataName("UserId"));

        // emit assembly and load from memory
        var memory = new MemoryStream();
        Assert.True(compilation.Emit(memory).Success);

        var assembly = Assembly.Load(memory.ToArray());

        var userId = assembly.GetExportedTypes().Single(t => t.Name == "UserId");

        Assert.Single(userId.GetInterfaces(), x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IComparable<>));

        var user1 = Activator.CreateInstance(userId, 1);
        var user2 = Activator.CreateInstance(userId, 1);

        Assert.Equal(0, userId.GetMethod("CompareTo")!.Invoke(user1, [user2]));
    }

    [Fact]
    public async Task GenerateComparableExplicitImpl()
    {
        var test = new GeneratorTest<TemplatedGenerator>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources =
                {
                    """
                    using System;
                    using StructId;

                    public readonly partial record struct UserId(Id Value) : IStructId<Id>;

                    public record struct Id(string value) : IComparable<Id>
                    {
                        int IComparable<Id>.CompareTo(Id other) => value.CompareTo(other.value);
                    }
                    """,
                    """
                    using System;
                    using StructId;

                    [TStructId]
                    file partial record struct TSelf(TId Value) : IComparable<TSelf>
                    {
                        // By casting to IComparable<TId> we can support explicit interface implementation too
                        public int CompareTo(TSelf other) => ((IComparable<TId>)Value).CompareTo(other.Value);
                    }

                    file record struct TId : IComparable<TId>
                    {
                        public int CompareTo(TId other) => throw new NotImplementedException();
                    }
                    """
                },
            },
        }.WithAnalyzerStructId();

        await test.RunAsync();

        // get compilation after generation run
        var compilation = test.Compilation;

        Assert.NotNull(compilation);
        Assert.NotNull(compilation.GetTypeByMetadataName("UserId"));

        // emit assembly and load from memory
        var memory = new MemoryStream();
        Assert.True(compilation.Emit(memory).Success);

        var assembly = Assembly.Load(memory.ToArray());

        var userId = assembly.GetExportedTypes().Single(t => t.Name == "UserId");
        var id = assembly.GetExportedTypes().Single(t => t.Name == "Id");

        Assert.Single(userId.GetInterfaces(), x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IComparable<>));

        var value = Activator.CreateInstance(id, "test");

        var user1 = Activator.CreateInstance(userId, value);
        var user2 = Activator.CreateInstance(userId, value);

        Assert.Equal(0, userId.GetMethod("CompareTo")!.Invoke(user1, [user2]));
    }

    class GeneratorTest<TSourceGenerator> : CSharpSourceGeneratorTest<TSourceGenerator, DefaultVerifier>
        where TSourceGenerator : new()
    {
        public Compilation? Compilation { get; private set; }

        protected override async Task<(Compilation compilation, ImmutableArray<Diagnostic> generatorDiagnostics)> GetProjectCompilationAsync(Project project, IVerifier verifier, CancellationToken cancellationToken)
        {
            var result = await base.GetProjectCompilationAsync(project, verifier, cancellationToken);

            Compilation = result.compilation;

            return result;
        }
    }
}
