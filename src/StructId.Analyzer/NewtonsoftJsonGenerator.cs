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

        // Extract just the namespace string from compilation — no ISymbol in pipeline value
        var source = context.CompilationProvider
            .Select((x, _) =>
            {
                var converter = x.GetTypeByMetadataName("Newtonsoft.Json.JsonConverter`1");
                if (converter == null)
                    return default((string?, bool));
                var known = new KnownTypes(x);
                return (known.StructIdNamespace, true);
            })
            .WithTrackingName(TrackingNames.NewtonsoftSource);

        context.RegisterSourceOutput(
            source,
            (context, source) =>
            {
                var (structIdNamespace, exists) = source;
                if (!exists || structIdNamespace == null)
                    return;

                context.AddSource("NewtonsoftJsonConverter.cs", SourceText.From(
                    ThisAssembly.Resources.Templates.NewtonsoftJsonConverter_1.Text
                    .Replace("namespace StructId;", $"namespace {structIdNamespace};")
                    .Replace("using StructId;", $"using {structIdNamespace};"),
                    Encoding.UTF8));
            });
    }
}