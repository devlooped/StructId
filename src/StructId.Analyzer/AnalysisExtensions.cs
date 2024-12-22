using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StructId;

public static class AnalysisExtensions
{
    public static SymbolDisplayFormat TypeName { get; } = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    public static SymbolDisplayFormat NamespacedTypeName { get; } = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    public static SymbolDisplayFormat FullNameNullable { get; } = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static string ToFullName(this ISymbol symbol) => symbol.ToDisplayString(FullNameNullable);

    public static CSharpParseOptions GetParseOptions(this Compilation compilation)
        => (CSharpParseOptions?)compilation.SyntaxTrees.FirstOrDefault()?.Options ??
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

    public static bool IsGeneratedByStructId(this ISymbol symbol)
        => symbol.GetAttributes().Any(a
            => a.AttributeClass?.Name == "GeneratedCodeAttribute" &&
               a.ConstructorArguments.Select(c => c.Value).OfType<string>().Any(v => v == nameof(StructId)));

    /// <summary>
    /// Checks whether the <paramref name="this"/> type inherits or implements the 
    /// <paramref name="baseTypeOrInterface"/> type, even if it's a generic type.
    /// </summary>
    public static bool Is(this ITypeSymbol? @this, ITypeSymbol? baseTypeOrInterface, bool looseGenerics = true)
    {
        if (@this == null || baseTypeOrInterface == null)
            return false;

        var fullName = @this.ToFullName();

        if (@this.Equals(baseTypeOrInterface, SymbolEqualityComparer.Default) == true)
            return true;

        if (baseTypeOrInterface is INamedTypeSymbol namedExpected &&
            @this is INamedTypeSymbol namedActual &&
            namedActual.IsGenericType &&
            (namedActual.ConstructedFrom.Equals(namedExpected, SymbolEqualityComparer.Default) ||
            // We optionally can consider a loose generic match based on the open generic.
            (looseGenerics && namedActual.ConstructedFrom.Equals(namedExpected.OriginalDefinition, SymbolEqualityComparer.Default))))
            return true;

        foreach (var iface in @this.AllInterfaces)
            if (iface.Is(baseTypeOrInterface, looseGenerics))
                return true;

        if (@this.BaseType?.Name.Equals("object", StringComparison.OrdinalIgnoreCase) == true &&
            @this.BaseType?.Equals(baseTypeOrInterface, SymbolEqualityComparer.Default) != true)
            return false;

        return Is(@this.BaseType, baseTypeOrInterface, looseGenerics);
    }

    public static bool ImplementsExplicitly(this INamedTypeSymbol namedTypeSymbol, INamedTypeSymbol interfaceTypeSymbol)
    {
        if (interfaceTypeSymbol.IsUnboundGenericType && interfaceTypeSymbol.TypeParameters.Length == 1)
        {
            try
            {
                interfaceTypeSymbol = interfaceTypeSymbol.ConstructedFrom.Construct(namedTypeSymbol);
            }
            catch { }
        }

        foreach (var interfaceMember in interfaceTypeSymbol.GetMembers())
        {
            foreach (var classMember in namedTypeSymbol.GetMembers())
            {
                switch (classMember)
                {
                    case IMethodSymbol methodSymbol:
                        if (methodSymbol.ExplicitInterfaceImplementations.Contains(interfaceMember, SymbolEqualityComparer.Default))
                            return true;
                        break;
                    case IPropertySymbol propertySymbol:
                        if (propertySymbol.ExplicitInterfaceImplementations.Contains(interfaceMember, SymbolEqualityComparer.Default))
                            return true;
                        break;
                    case IEventSymbol eventSymbol:
                        if (eventSymbol.ExplicitInterfaceImplementations.Contains(interfaceMember, SymbolEqualityComparer.Default))
                            return true;
                        break;
                }
            }
        }
        return false;
    }

    public static IEnumerable<INamedTypeSymbol> GetAllTypes(this Compilation compilation, bool includeReferenced = true)
        => compilation.Assembly
            .GetAllTypes()
            .OfType<INamedTypeSymbol>()
            .Concat(!includeReferenced ? [] : compilation.GetUsedAssemblyReferences()
            .SelectMany(r =>
            {
                if (compilation.GetAssemblyOrModuleSymbol(r) is IAssemblySymbol asm)
                    return asm.GetAllTypes().OfType<INamedTypeSymbol>();

                return [];
            }));

    public static IEnumerable<INamedTypeSymbol> GetAllTypes(this IAssemblySymbol assembly)
        => GetAllTypes(assembly.GlobalNamespace);

    static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var typeSymbol in namespaceSymbol.GetTypeMembers())
            yield return typeSymbol;

        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var typeSymbol in GetAllTypes(childNamespace))
            {
                yield return typeSymbol;
            }
        }
    }

    public static string GetTypeName(this ITypeSymbol type, string? containingNamespace)
    {
        var typeName = type.ToDisplayString(FullNameNullable);
        if (containingNamespace == null)
            return typeName;

        if (typeName.StartsWith(containingNamespace + "."))
            return typeName[(containingNamespace.Length + 1)..];

        return typeName;
    }

    public static string ToFileName(this ITypeSymbol type)
    {
        if (type.ContainingNamespace == null || type.ContainingNamespace.IsGlobalNamespace)
            return type.Name;

        var name = type.MetadataName.Replace('+', '.');
        return $"{type.ContainingNamespace.ToFullName()}.{name}";
    }

    public static bool IsStructId(this ITypeSymbol type) => type.AllInterfaces.Any(x => x.Name == "IStructId");

    public static bool IsValueTemplate(this INamedTypeSymbol symbol)
        => symbol.GetAttributes().Any(IsValueTemplate);

    public static bool IsValueTemplate(this AttributeData attribute)
        => attribute.AttributeClass?.Name == "TValue" ||
           attribute.AttributeClass?.Name == "TValueAttribute";

    public static bool IsValueTemplate(this AttributeSyntax attribute)
        => attribute.Name.ToString() == "TValue" || attribute.Name.ToString() == "TValueAttribute";

    public static bool IsStructIdTemplate(this INamedTypeSymbol symbol)
        => symbol.GetAttributes().Any(IsStructIdTemplate);

    public static bool IsStructIdTemplate(this AttributeData attribute)
        => attribute.AttributeClass?.Name == "TStructId" ||
           attribute.AttributeClass?.Name == "TStructIdAttribute";

    public static bool IsStructIdTemplate(this AttributeSyntax attribute)
        => attribute.Name.ToString() == "TStructId" || attribute.Name.ToString() == "TStructIdAttribute";

    public static bool IsPartial(this ITypeSymbol node) => node.DeclaringSyntaxReferences.Any(
        r => r.GetSyntax() is TypeDeclarationSyntax { Modifiers: { } modifiers } &&
            modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

    public static bool IsFileLocal(this ITypeSymbol? node) => node != null && node.DeclaringSyntaxReferences.All(
        r => r.GetSyntax() is TypeDeclarationSyntax { Modifiers: { } modifiers } &&
            modifiers.Any(m => m.IsKind(SyntaxKind.FileKeyword)));
}
