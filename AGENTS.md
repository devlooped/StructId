# StructId – Agent Implementation Notes

This document captures the design decisions, architecture, and implementation details of StructId for use by future agents working in this repository.

---

## Project Overview

StructId is a zero-dependency, strongly-typed ID library for .NET. Every user-declared ID type is a `readonly partial record struct` that implements either `IStructId` (string-backed) or `IStructId<TValue>` (any struct-backed value). All generated code is emitted directly into the consuming project via Roslyn incremental source generators, so there are **no runtime package references** in the output.

Key design principles:

- **Zero runtime dependencies** – the NuGet package is `developmentDependency="true"`. The generated code is self-contained inside the user's project.
- **Zero configuration** – additional integrations (EF Core, Dapper, Newtonsoft.Json, …) activate automatically when the corresponding packages are referenced by the consuming project.
- **Newest C# features** – `readonly record struct`, `IParsable<T>`, `ISpanParsable<T>`, static interface members, file-scoped types, primary constructors.
- **Extensible via compiled C# templates** – the same template mechanism used internally is fully available to library consumers.

---

## Repository Structure

```
/
├── src/
│   ├── StructId/                   # Core runtime interfaces and shared helpers (embedded as resources)
│   ├── StructId.Analyzer/          # All incremental source generators and analyzers
│   ├── StructId.CodeFix/           # Roslyn code-fix providers
│   ├── StructId.Tests/             # Unit tests (Roslyn source generator tests + incrementality tests)
│   ├── StructId.FunctionalTests/   # End-to-end tests against real EF Core / Dapper / JSON
│   ├── StructId.Package/           # Packaging project (produces the NuGet package)
│   └── Sample/                     # Sample applications (MvcWebApp, MinimalApi, Console)
├── docs/                           # Diagnostic documentation (SID001.md … SID005.md)
├── readme.md                       # Public-facing documentation
├── changelog.md
└── AGENTS.md                       # This file
```

### Project roles

| Project | Role |
|---|---|
| `StructId` | Defines the core interfaces (`IStructId`, `IStructId<TValue>`, `INewable<TSelf>`, `INewable<TSelf,TValue>`), shared converters (`StructIdConverters`), and template source files. Its files are embedded as resources in `StructId.Analyzer` so generators can read them at compile time. |
| `StructId.Analyzer` | All `IIncrementalGenerator` implementations, diagnostic analyzers, and the `CodeTemplate` engine. This is the main engine of the library. |
| `StructId.CodeFix` | `CodeFixProvider` implementations that accompany the analyzers (e.g., add `partial`, `readonly`, `record struct`; rename/remove custom constructor parameters). |
| `StructId.Tests` | Unit tests using `Microsoft.CodeAnalysis.Testing` (Roslyn verifier pattern). Tests cover generated output, diagnostics, code fixes, and generator incrementality. |
| `StructId.FunctionalTests` | Integration tests that run against real SQLite databases via EF Core and Dapper, real JSON serialization, etc. |
| `StructId.Package` | MSBuild packaging project that bundles the analyzer DLL, the `StructId.targets` file, and the generated package readme. |

---

## Core Interfaces

All interfaces live in `src/StructId/` and are embedded as text resources into `StructId.Analyzer`. The generator uses `ThisAssembly.Resources.StructId.*` to read them.

### `IStructId` (string-backed IDs)

```csharp
public partial interface IStructId
{
    string Value { get; }
}
```

### `IStructId<TValue>` (struct-backed IDs)

```csharp
public partial interface IStructId<TValue> where TValue : struct
{
    TValue Value { get; }
}
```

### `INewable<TSelf>` / `INewable<TSelf, TValue>`

These interfaces provide a consistent static factory pattern:

```csharp
public interface INewable<TSelf>
{
    public abstract static TSelf New(string value);
}

public interface INewable<TSelf, TValue>
{
    public abstract static TSelf New(TValue value);
}
```

All struct IDs automatically implement these via generated templates (see `src/StructId/Templates/Newable.cs`, `NewableT.cs`, `NewableGuid.cs`, `NewableUlid.cs`).

### `TStructIdAttribute` / `TValueAttribute`

```csharp
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class TStructIdAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class TValueAttribute : Attribute { }
```

`[TStructId]` marks a file-local `partial record struct TSelf` as a template that will be applied to every matching struct ID. `[TValue]` marks a file-local type that defines a custom Dapper/EF handler for a specific value type.

---

## Generator Architecture

### `BaseGenerator`

`BaseGenerator` is the abstract base for all feature generators except `TemplatedGenerator`. It is constructed with:

| Parameter | Purpose |
|---|---|
| `referenceType` | Fully-qualified metadata name of a type that must exist in the compilation (checked via `Compilation.GetTypeByMetadataName`). Used as the activation gate. |
| `stringTemplate` | Embedded resource template text applied to string-backed (`IStructId`) struct IDs. |
| `typeTemplate` | Embedded resource template text applied to typed (`IStructId<TValue>`) struct IDs. |
| `referenceCheck` | `ValueIsType` (default) – the value type of the struct ID must implement `referenceType`. `TypeExists` – the reference type just needs to be present in the compilation. |

**Pipeline (simplified):**

```
SyntaxProvider (RecordDeclarationSyntax with IStructId base)
  → ExtractStructIdModel
  → .Combine(refType) → filter by ReferenceCheck
  → OnInitialize (override hook for subclasses)
  → RegisterImplementationSourceOutput → GenerateCode
```

`GenerateCode` calls `CodeTemplate.Apply` with the appropriate template and emits one `.cs` file per struct ID.

Subclasses override `OnInitialize` to add extra pipeline steps (e.g., collecting custom converters) or `SelectTemplate` to pick different templates per struct ID.

### `TemplatedGenerator`

Handles user-defined (and built-in) `[TStructId]`-annotated template types. It does not extend `BaseGenerator`.

**Pipeline:**

1. `CompilationProvider.Select` – discovers all `[TStructId]` file-local `partial record struct TSelf` types in the compilation (including referenced assemblies) and converts them to `TemplateModel` records.
2. `SyntaxProvider.CreateSyntaxProvider` – discovers all `IStructId`-implementing `RecordDeclarationSyntax` nodes and converts them to `StructIdModel` records.
3. Cross-product via `.Combine` + `.SelectMany` – pairs each struct ID with every template that `AppliesTo` it.
4. `RegisterSourceOutput` → `GenerateCode` – calls `CodeTemplate.Apply` and emits one file per `(structId, template)` pair, using the hint name `{StructId.FileName}/{templateFile}.cs`.

### Built-in Generators

| Generator | Reference Type | String template | Typed template | Notes |
|---|---|---|---|---|
| `ConstructorGenerator` | `System.Object` (TypeExists) | `Templates/Constructor.cs` | `Templates/ConstructorT.cs` | Only emits if the struct ID does **not** already have a primary constructor (`HasParameterList == false`). |
| `SystemTextJsonGenerator` | `System.IParsable<T>` (ValueIsType) | `Templates/JsonConverter.cs` | `Templates/JsonConverterT.cs` | Value type must implement `IParsable<T>`. |
| `NewtonsoftJsonGenerator` | `Newtonsoft.Json.JsonConverter<T>` (TypeExists) | `Templates/NewtonsoftJsonConverter.cs` | `Templates/NewtonsoftJsonConverterT.cs` | Activated when Newtonsoft.Json is referenced. |
| `EntityFrameworkGenerator` | `Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<,>` (TypeExists) | `Templates/EntityFramework.cs` | `Templates/EntityFramework.cs` or `EntityFrameworkParsable.cs` | See notes below. |
| `DapperGenerator` | `Dapper.SqlMapper+TypeHandler<T>` (TypeExists) | `DapperExtensions.sbn` (Scriban) | same | See notes below. |

**EntityFrameworkGenerator** additionally scans for user-defined `ValueConverter<TModel,TProvider>` subclasses and `[TValue]`-annotated template types, registering all of them in the generated `UseStructId` extension method on `DbContextOptionsBuilder`. For value types not natively supported by EF Core (i.e., not in the built-in primitive set), it uses `EntityFrameworkParsable.cs` if the type implements `IParsable<T>` and `IFormattable`.

**DapperGenerator** uses Scriban rather than `CodeTemplate` because it must generate a single aggregated file listing all struct IDs together (needed for the `UseStructId` `IDbConnection` extension). Built-in handler support covers `Guid`, `int`, `long`, and `string`; all other types fall back to persisting as strings via `IParsable<T>` + `IFormattable`.

---

## Template System

### `CodeTemplate`

The `CodeTemplate` static class in `StructId.Analyzer/CodeTemplate.cs` is the core of the template expansion engine. It takes a C# source file (the template) and replaces the placeholder identifiers `TSelf` and `TValue` with the concrete names of the target struct ID and its value type.

**Key operations:**

- `CodeTemplate.Parse(text)` – parses template text into a `SyntaxNode` via `CSharpSyntaxTree.ParseText`.
- `CodeTemplate.Apply(template, typeName, valueType, targetNamespace, coreNamespace)` – applies both `TSelf` and `TValue` substitutions, wraps the output in the correct file-scoped namespace, and deduplicates `using` directives.
- `CodeTemplate.Apply(template, valueType)` – applies only `TValue` substitution (used for `[TValue]` templates that generate value-type helpers).

**`TemplateRewriter`** (inner `CSharpSyntaxRewriter`):

- Removes file-local types that are **not** annotated with `[TStructId]` (they are constraint helpers, not output code).
- Removes the primary constructor from the `[TStructId]` type (the struct ID's own constructor is provided by `ConstructorGenerator`).
- Removes the `[TStructId]` attribute itself.
- Removes the `file` modifier from type declarations.
- Replaces all identifier tokens `TSelf` → actual struct ID name; `TValue` (or `TId`, legacy) → actual value type name.
- Supports prefixed identifiers (`TSelf_Foo` → `ActualName_Foo`, `TValue_Bar` → `ActualValue_Bar`) for generated helper type names.

**`ValueRewriter`** (inner `CSharpSyntaxRewriter`):

- Used for `[TValue]` templates; removes file-local types not annotated with `[TValue]`.
- Replaces `TValue` identifiers with the concrete value type name.

**Namespace handling:**

- If the struct ID has a namespace, the output is wrapped in a file-scoped namespace declaration.
- The `using StructId;` directive in templates is rewritten to use the actual `CoreNamespace` (supports scenarios where StructId is embedded in another namespace).

### `[TStructId]` Template Rules

1. Must be a `file partial record struct`.
2. Must be named `TSelf`.
3. Primary constructor (if present) must have a single parameter named `Value`. Its type constrains which struct IDs the template applies to:
   - `string Value` → applies only to string-backed IDs.
   - Concrete type (e.g., `Guid Value`) → applies only to IDs whose value type **is** `Guid`.
   - `TValue Value` (with a companion `file record struct TValue`) → applies to IDs whose value type satisfies all interfaces declared on the `TValue` helper. Leave `TValue` empty to match any value type.
   - `/*!string*/ TValue Value` inline comment → excludes string-backed IDs from matching.
4. Additional partial declarations of `TSelf` (without the `[TStructId]` attribute) can declare interface constraints for further filtering.

### `TemplateModel` / `StructIdModel` (Cacheable Models)

`CacheableModels.cs` defines plain-data record structs (`StructIdModel`, `TemplateModel`, `TValueTemplateModel`, `TemplatizedValueOutput`) that contain only strings and `EquatableArray<string>`. These are used throughout the incremental pipeline to avoid carrying `ISymbol` or `Compilation` references, which break incremental caching.

`ModelExtractors` provides static factory methods that extract these models from Roslyn symbols inside pipeline transform lambdas.

`TemplateModel.AppliesTo(StructIdModel)` implements the matching logic:
- Exact value type match.
- Value type implements the template's `TValueFullName`.
- For file-local `TValue` constraints: value type implements all interfaces declared on the file-local `TValue`.

### `[TValue]` Templates and `TemplatizedTValueExtensions`

`[TValue]`-annotated file-local types declare custom Dapper handlers or EF Core value converters for specific value types. `TemplatizedTValueExtensions.SelectTemplatizedValues` extracts these and produces `TemplatizedValueOutput` records (applied code + type name), which are then consumed by `DapperGenerator` and `EntityFrameworkGenerator` to register additional handlers/converters.

---

## Diagnostics and Code Fixes

### Diagnostics

| ID | Analyzer | Trigger | Severity |
|---|---|---|---|
| SID001 | `RecordAnalyzer` | Struct ID is not a `partial readonly record struct`. | Error |
| SID002 | `RecordAnalyzer` | Custom primary constructor parameter is not named `Value` (or has multiple parameters). | Error |
| SID003 | `TemplateAnalyzer` | `[TStructId]` type is not a file-local `partial record struct`. | Error |
| SID004 | `TemplateAnalyzer` | `[TStructId]` template constructor parameter is not named `Value`. | Error |
| SID005 | `TemplateAnalyzer` | `[TStructId]` template type is not named `TSelf`. | Error |

### Code Fixes

| Fix | Targets | Action |
|---|---|---|
| `RecordCodeFix` | SID001 | Adds missing `partial`, `readonly`, `record struct` modifiers. |
| `RenameCtorCodeFix` | SID002 | Renames the constructor parameter to `Value`. |
| `RemoveCtorCodeFix` | SID002 | Removes the custom constructor entirely. |
| `TemplateCodeFix` | SID003, SID005 | Adds `file` modifier and/or renames type to `TSelf`. |

---

## Incremental Generation and Caching

All generators use the Roslyn incremental generator API (`IIncrementalGenerator`) with named tracking steps (`WithTrackingName`) so that incrementality can be verified in tests.

### `TrackingNames`

```csharp
static class TrackingNames
{
    public const string ReferenceType = ...;
    public const string StructIds = ...;
    public const string Combined = ...;
    public const string Templates = ...;
    public const string TemplatizedStructIds = ...;
    public const string BuiltInHandled = ...;
    public const string CustomHandlers = ...;
    public const string TemplatizedValues = ...;
    public const string Converters = ...;
    public const string NewtonsoftSource = ...;
    public const string TValueTemplates = ...;
    public const string TValueValues = ...;
}
```

### `EquatableArray<T>`

A value-type wrapper around `ImmutableArray<T>` that provides structural equality. Used in all cacheable model records to ensure that incremental pipeline steps correctly detect whether their inputs have changed.

---

## Testing Patterns

### Test Framework

- **xUnit** with `ITestOutputHelper` injection for diagnostic output.
- **Moq** for mocking (where applicable).
- **Microsoft.CodeAnalysis.Testing** (`CSharpSourceGeneratorTest<TGenerator, DefaultVerifier>`) for verifying generator output and diagnostics.

### `StructIdGeneratorTest<TGenerator>`

A custom base class in `StructIdGeneratorTest.cs` that always includes `TemplatedGenerator` and `ConstructorGenerator` in the generator set, ensuring templates are applied correctly in tests for other generators.

### Reference Assemblies

Tests use `ReferenceAssemblies.Net.Net80` for compilation references. File paths must be provided on syntax trees for file-local types to work correctly (required by Roslyn's file-local type resolution).

### Incrementality Tests

`IncrementalityTests.cs` verifies that the incremental pipeline does not unnecessarily re-run steps when inputs have not changed. Use:

```
dotnet test --filter "FullyQualifiedName~IncrementalityTests"
```

`IncrementalityTestHelpers.CreateCompilation` sets up a `CSharpCompilation` with the StructId core source files and .NET 8 references.

### Functional Tests

`StructId.FunctionalTests` exercises the full code generation pipeline end-to-end with real SQLite databases (via EF Core and Dapper), real JSON serialization, and real TypeConverter usage. Run with `dnx --yes retest` from the repo root.

---

## Build Commands

| Command | Purpose |
|---|---|
| `dotnet restore` | Restore all NuGet dependencies. |
| `dotnet build` | Build the entire solution. |
| `dnx --yes retest` | Run all tests with automatic retry on transient failures (preferred). |
| `dotnet format whitespace -v:diag --exclude ~/.nuget` | Fix whitespace formatting. |
| `dotnet format style -v:diag --exclude ~/.nuget` | Fix style formatting. |
| `dotnet format whitespace --verify-no-changes -v:diag --exclude ~/.nuget` | Verify formatting (CI mode). |

CI runs `dnx --yes retest -- --no-build` (skips build, runs tests only).

---

## Key Design Decisions

### Why `readonly record struct`?

`readonly record struct` provides:
- Value semantics (equality, `GetHashCode`, `ToString`) generated by the compiler.
- Zero heap allocation (stack-allocated value type).
- `IEquatable<T>` implementation for free.
- C# primary constructor syntax for the required `Value` property.

### Why file-scoped templates?

File-scoped C# types (`file` keyword) are automatically invisible outside the file, preventing template helper types from leaking into the consuming assembly. Templates are valid compilable C# files, giving full IDE support (IntelliSense, syntax highlighting, error checking) during template authoring.

### Why no runtime package references?

Making the package `developmentDependency="true"` means consumers can use StructId without adding a transitive dependency to their library or application packages. All generated code is part of the consuming project.

### Embedded Resources vs. File-system Templates

Built-in templates (in `src/StructId/Templates/` and `src/StructId/ResourceTemplates/`) are embedded as text resources into `StructId.Analyzer` via MSBuild. This means the generator can read them at compile time without any file I/O. User-defined templates are read directly from the Roslyn compilation's syntax trees.

### `ReferenceCheck.TypeExists` vs. `ReferenceCheck.ValueIsType`

- `ValueIsType` (default): The struct ID's value type must implement the reference type. Used for `SystemTextJsonGenerator` (value type must implement `IParsable<T>`).
- `TypeExists`: The reference type just needs to appear in the compilation (i.e., the library is referenced). Used for `DapperGenerator`, `EntityFrameworkGenerator`, `NewtonsoftJsonGenerator` — all struct IDs in the project get handlers when the library is present.

### Dapper uses Scriban, others use CodeTemplate

`DapperGenerator` produces a single aggregated `DapperExtensions.cs` file that lists all struct IDs together (for the `UseStructId` registration). This requires conditional and loop logic not available in the CodeTemplate Roslyn-rewrite approach, so Scriban (a text templating engine) is used instead.

---

## Adding a New Integration Generator

1. Add embedded resource templates to `src/StructId/ResourceTemplates/` (for string-backed and typed IDs, if they differ).
2. Create a new `[Generator]` class in `src/StructId.Analyzer/` that extends `BaseGenerator`, passing the activation reference type and template texts.
3. Override `OnInitialize` if custom pipeline steps are needed (e.g., collecting additional symbols from the compilation).
4. Override `SelectTemplate` if different templates are needed for different value types.
5. Add tests in `src/StructId.Tests/` following the existing `StructIdGeneratorTest<TGenerator>` pattern.
6. Add functional tests in `src/StructId.FunctionalTests/` if the integration can be exercised end-to-end.
