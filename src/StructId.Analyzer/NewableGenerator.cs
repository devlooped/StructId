using Microsoft.CodeAnalysis;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class NewableGenerator() : TemplateGenerator(
    "System.Object",
    ThisAssembly.Resources.Templates.Newable.Text,
    ThisAssembly.Resources.Templates.NewableT.Text,
    TypeCheck.TypeExists);