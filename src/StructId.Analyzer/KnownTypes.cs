using System.Linq;
using Microsoft.CodeAnalysis;

namespace StructId;

/// <summary>
/// Provides access to some common types and properties used in the compilation.
/// </summary>
/// <param name="Compilation">The compilation used to resolve the known types.</param>
public record KnownTypes(Compilation Compilation)
{
    public string StructIdNamespace => IStructId?.ContainingNamespace.ToFullName() ?? "StructId";

    /// <summary>
    /// System.String
    /// </summary>
    public INamedTypeSymbol String { get; } = Compilation.GetTypeByMetadataName("System.String")!;

    /// <summary>
    /// StructId.IStructId
    /// </summary>
    public INamedTypeSymbol? IStructId { get; } = Compilation
        .GetAllTypes(includeReferenced: true)
        .FirstOrDefault(x => x.MetadataName == "IStructId" && x.IsGeneratedByStructId());

    /// <summary>
    /// StructId.IStructId{T}
    /// </summary>
    public INamedTypeSymbol? IStructIdT { get; } = Compilation
        .GetAllTypes(includeReferenced: true)
        .FirstOrDefault(x => x.MetadataName == "IStructId`1" && x.IsGeneratedByStructId());
}