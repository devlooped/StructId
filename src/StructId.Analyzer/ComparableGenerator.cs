using Microsoft.CodeAnalysis;

namespace StructId;

[Generator(LanguageNames.CSharp)]
public class ComparableGenerator() : BaseGenerator(
    "System.IComparable`1",
    ThisAssembly.Resources.Templates.Comparable.Text,
    ThisAssembly.Resources.Templates.Comparable.Text,
    ReferenceCheck.ValueIsType);