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
    public static SymbolDisplayFormat FullName { get; } = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable);

    /// <summary>
    /// Checks whether the <paramref name="this"/> type inherits or implements the 
    /// <paramref name="baseTypeOrInterface"/> type, even if it's a generic type.
    /// </summary>
    public static bool Is(this ITypeSymbol? @this, ITypeSymbol? baseTypeOrInterface)
    {
        if (@this == null || baseTypeOrInterface == null)
            return false;

        if (@this.Equals(baseTypeOrInterface, SymbolEqualityComparer.Default) == true)
            return true;

        if (baseTypeOrInterface is INamedTypeSymbol namedExpected &&
            @this is INamedTypeSymbol namedActual &&
            namedActual.IsGenericType &&
            (namedActual.ConstructedFrom.Equals(namedExpected, SymbolEqualityComparer.Default) ||
            namedActual.ConstructedFrom.Equals(namedExpected.OriginalDefinition, SymbolEqualityComparer.Default)))
            return true;

        foreach (var iface in @this.AllInterfaces)
            if (iface.Is(baseTypeOrInterface))
                return true;

        if (@this.BaseType?.Name.Equals("object", StringComparison.OrdinalIgnoreCase) == true)
            return false;

        return Is(@this.BaseType, baseTypeOrInterface);
    }

    public static string GetStructIdNamespace(this AnalyzerConfigOptions options)
        => options.TryGetValue("build_property.StructIdNamespace", out var ns) && !string.IsNullOrEmpty(ns) ? ns : "StructId";

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
        var typeName = type.ToDisplayString(FullName);
        if (containingNamespace == null)
            return typeName;

        if (typeName.StartsWith(containingNamespace + "."))
            return typeName[(containingNamespace.Length + 1)..];

        return typeName;
    }

    public static string ToFileName(this ITypeSymbol type) => type.ToDisplayString(FullName).Replace('+', '.');

    public static bool IsStructId(this ITypeSymbol type) => type.AllInterfaces.Any(x => x.Name == "IStructId");

    public static bool IsPartial(this ITypeSymbol node) => node.DeclaringSyntaxReferences.Any(
        r => r.GetSyntax() is TypeDeclarationSyntax { Modifiers: { } modifiers } &&
            modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
}
