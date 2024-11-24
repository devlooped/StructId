using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class ParsableGenerator() : TemplateGenerator(
    "System.IParsable`1",
    ThisAssembly.Resources.Templates.SParsable.Text,
    ThisAssembly.Resources.Templates.TParsable.Text);