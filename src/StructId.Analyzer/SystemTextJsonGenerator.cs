using Microsoft.CodeAnalysis;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class SystemTextJsonGenerator() : TemplateGenerator(
    "System.IParsable`1",
    ThisAssembly.Resources.Templates.JsonConverter.Text,
    ThisAssembly.Resources.Templates.JsonConverterT.Text);