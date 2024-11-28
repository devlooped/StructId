using Microsoft.CodeAnalysis;

namespace StructId;

public static class Diagnostics
{
    public static DiagnosticDescriptor MustBeRecordStruct { get; } = new(
        "SID001",
        "Struct ids must be partial readonly record structs",
        "Change '{0}' to a partial readonly record struct as required for types used as struct ids.",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: $"{ThisAssembly.Project.RepositoryUrl}/blob/{ThisAssembly.Project.RepositoryBranch}/docs/SID001.md");

    public static DiagnosticDescriptor MustHaveValueConstructor { get; } = new(
        "SID002",
        "Struct id custom constructor must provide a single Value parameter",
        "Custom constructor for '{0}' must have a Value parameter",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: $"{ThisAssembly.Project.RepositoryUrl}/blob/{ThisAssembly.Project.RepositoryBranch}/docs/SID002.md");
}