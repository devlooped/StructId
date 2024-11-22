using Microsoft.CodeAnalysis;

namespace StructId;

public static class Diagnostics
{
    /// <summary>
    /// SID001: StructId must be a partial readonly record struct.
    /// </summary>
    public static DiagnosticDescriptor MustBeRecordStruct { get; } = new(
        "SID001",
        "Struct ids must be partial readonly record structs",
        "Change '{0}' to a partial readonly record struct as required for types used as struct ids.",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true, 
        helpLinkUri: $"{ThisAssembly.Project.RepositoryUrl}/blob/{ThisAssembly.Project.RepositoryBranch}/docs/SID001.md");
}
