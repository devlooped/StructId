using Microsoft.CodeAnalysis;

namespace StructId;

/// <summary>
/// Provides access to some common types and properties used in the compilation.
/// </summary>
/// <param name="Compilation">The compilation used to resolve the known types.</param>
/// <param name="StructIdNamespace">The namespace for StructId types.</param>
public record KnownTypes(Compilation Compilation, string StructIdNamespace)
{
    /// <summary>
    /// System.String
    /// </summary>
    public INamedTypeSymbol String { get; } = Compilation.GetTypeByMetadataName("System.String")!;
    /// <summary>
    /// StructId.IStructId
    /// </summary>
    public INamedTypeSymbol? IStructId { get; } = Compilation.GetTypeByMetadataName($"{StructIdNamespace}.IStructId");
    /// <summary>
    /// StructId.IStructId{T}
    /// </summary>
    public INamedTypeSymbol? IStructIdT { get; } = Compilation.GetTypeByMetadataName($"{StructIdNamespace}.IStructId`1");
    /// <summary>
    /// StructId.TStructIdAttribute
    /// </summary>
    public INamedTypeSymbol? TStructId { get; } = Compilation.GetTypeByMetadataName($"{StructIdNamespace}.TStructIdAttribute");
}