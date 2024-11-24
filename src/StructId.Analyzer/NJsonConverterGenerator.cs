using Microsoft.CodeAnalysis;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class NJsonConverterGenerator() : TemplateGenerator(
    "Newtonsoft.Json.JsonConverter",
    ThisAssembly.Resources.Templates.NJsonConverter.Text, 
    ThisAssembly.Resources.Templates.NJsonConverterT.Text,
    TypeCheck.TypeExists);