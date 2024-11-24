using Microsoft.CodeAnalysis;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class JsonConverterGenerator() : TemplateGenerator(
    "System.IParsable`1",
    ThisAssembly.Resources.Templates.SJsonConverter.Text, 
    ThisAssembly.Resources.Templates.TJsonConverter.Text);