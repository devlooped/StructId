using Microsoft.CodeAnalysis;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class ConversionGenerator() : TemplateGenerator(
    "System.Object",
    ThisAssembly.Resources.Templates.Conversion.Text,
    ThisAssembly.Resources.Templates.ConversionT.Text,
    ReferenceCheck.TypeExists);