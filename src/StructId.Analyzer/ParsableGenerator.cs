using Microsoft.CodeAnalysis;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class ParsableGenerator() : BaseGenerator(
    "System.IParsable`1",
    ThisAssembly.Resources.Templates.Parsable.Text,
    ThisAssembly.Resources.Templates.ParsableT.Text);