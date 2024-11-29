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

        context.RegisterSourceOutput(
            context.CompilationProvider
            .Select((x, _) => x.GetTypeByMetadataName("Newtonsoft.Json.JsonConverter`1")),
            (context, source) =>
            {
                if (source == null)
                    return;

                context.AddSource("NewtonsoftJsonConverter.cs", SourceText.From(
                    ThisAssembly.Resources.Templates.NewtonsoftJsonConverter_1.Text, Encoding.UTF8));
            });
    }
}