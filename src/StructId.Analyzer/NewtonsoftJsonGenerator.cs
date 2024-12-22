using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class NewtonsoftJsonGenerator() : BaseGenerator(
    "Newtonsoft.Json.JsonConverter`1",
    ThisAssembly.Resources.Templates.NewtonsoftJsonConverter.Text,
    ThisAssembly.Resources.Templates.NewtonsoftJsonConverterT.Text,
    ReferenceCheck.TypeExists)
{
    public override void Initialize(IncrementalGeneratorInitializationContext context)
    {
        base.Initialize(context);

        var source = context.CompilationProvider
            .Select((x, _) => (new KnownTypes(x), x.GetTypeByMetadataName("Newtonsoft.Json.JsonConverter`1")));

        context.RegisterSourceOutput(
            source,
            (context, source) =>
            {
                (var known, var converter) = source;
                if (converter == null)
                    return;

                context.AddSource("NewtonsoftJsonConverter.cs", SourceText.From(
                    ThisAssembly.Resources.Templates.NewtonsoftJsonConverter_1.Text
                    .Replace("namespace StructId;", $"namespace {known.StructIdNamespace};")
                    .Replace("using StructId;", $"using {known.StructIdNamespace};"),
                    Encoding.UTF8));
            });
    }
}