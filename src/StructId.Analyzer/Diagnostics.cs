using Microsoft.CodeAnalysis;

namespace StructId;

public static class Diagnostics
{
    public static DiagnosticDescriptor MustBeRecordStruct { get; } = new(
        "SID001",
        "Struct Ids must be partial readonly record structs",
        "'{0}' must be a partial readonly record struct to be a struct ids.",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: $"{ThisAssembly.Project.RepositoryUrl}/blob/{ThisAssembly.Project.RepositoryBranch}/docs/SID001.md");

    public static DiagnosticDescriptor MustHaveValueConstructor { get; } = new(
        "SID002",
        "Struct Id custom constructor must provide a single Value parameter",
        "Custom constructor for '{0}' must have a Value parameter",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: $"{ThisAssembly.Project.RepositoryUrl}/blob/{ThisAssembly.Project.RepositoryBranch}/docs/SID002.md");

    public static DiagnosticDescriptor TemplateMustBeFileRecordStruct { get; } = new(
        "SID003",
        "Struct Id templates must be file-local partial record structs",
        "'{0}' must be a file-local partial record struct to be used as a template.",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: $"{ThisAssembly.Project.RepositoryUrl}/blob/{ThisAssembly.Project.RepositoryBranch}/docs/SID003.md");

    public static DiagnosticDescriptor TemplateConstructorValueConstructor { get; } = new(
        "SID004",
        "Struct Id template constructor must provide a single Value parameter",
        "Custom template constructor must have a single Value parameter, if present",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: $"{ThisAssembly.Project.RepositoryUrl}/blob/{ThisAssembly.Project.RepositoryBranch}/docs/SID004.md");

    public static DiagnosticDescriptor TemplateDeclarationNotTSelf { get; } = new(
        "SID005",
        "Struct Id template declaration must use the reserved name 'TSelf'",
        "'{0}' must be named 'TSelf' to be used as a template.",
        "Build",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: $"{ThisAssembly.Project.RepositoryUrl}/blob/{ThisAssembly.Project.RepositoryBranch}/docs/SID005.md");
}