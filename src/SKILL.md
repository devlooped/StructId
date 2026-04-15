---
name: structid
description: >
  Helps define, use, and extend StructId — a zero-dependency, strongly-typed ID library for .NET
  that uses readonly record structs. Use this skill when working with struct IDs, value-typed 
  identifiers, IStructId, IStructId<TValue>, EF Core converters, Dapper handlers, JSON converters, 
  custom templates ([TStructId]/[TValue]), or INewable factory patterns in a StructId-based project.
---

# StructId

StructId is a zero-dependency, strongly-typed ID library for .NET. Every user-declared ID type is a 
`readonly partial record struct` that implements either `IStructId` (string-backed) or 
`IStructId<TValue>` (struct-backed). All code is source-generated directly into the consuming 
project — there are **no runtime package references**.

## Core Interfaces

```csharp
using StructId;

// String-backed ID
public readonly partial record struct ProductId : IStructId;

// Struct-backed ID (Guid, int, long, Ulid, or any struct)
public readonly partial record struct UserId : IStructId<Guid>;
public readonly partial record struct OrderId : IStructId<int>;
```

### `IStructId` (string value)

```csharp
namespace StructId;

public partial interface IStructId
{
    string Value { get; }
}
```

### `IStructId<TValue>` (struct value)

```csharp
namespace StructId;

public partial interface IStructId<TValue> where TValue : struct
{
    TValue Value { get; }
}
```

### `INewable<TSelf>` / `INewable<TSelf, TValue>` (factory pattern)

Static interface members for consistent factory methods:

```csharp
namespace StructId;

public interface INewable<TSelf>
{
    static abstract TSelf New(string value);
}

public interface INewable<TSelf, TValue>
{
    static abstract TSelf New(TValue value);
}
```

All struct IDs automatically implement these interfaces via generated code. Use them for 
generic constraints that require creating new instances:

```csharp
T CreateId<T>(string value) where T : INewable<T> => T.New(value);
T CreateId<T, V>(V value) where T : INewable<T, V> => T.New(value);
```

## Declaring Struct IDs

The minimum declaration is a `readonly partial record struct` implementing one of the core interfaces:

```csharp
using StructId;

public readonly partial record struct UserId : IStructId<Guid>;
public readonly partial record struct ProductId : IStructId;       // string-backed
public readonly partial record struct OrderId : IStructId<int>;
public readonly partial record struct TraceId : IStructId<Ulid>;   // Ulid supported out of the box
```

**Key requirements** (enforced by analyzer with code fixes):
- Must be `readonly`
- Must be `partial`
- Must be `record struct`
- If you declare a primary constructor, it must have a single parameter named `Value`

```csharp
using StructId;

// Custom primary constructor (e.g. to add attributes)
public readonly partial record struct ProductId([property: JsonPropertyName("id")] int Value) : IStructId<int>;
```

## What Gets Generated

For every struct ID, the source generator emits:
- Primary constructor `(TValue Value)` (unless you declared one)
- `Value` property
- `IComparable<TSelf>` + comparison operators (`<`, `<=`, `>`, `>=`) if `TValue : IComparable<TValue>`
- `IParsable<TSelf>` + `ISpanParsable<TSelf>` if `TValue : IParsable<TValue>`
- `IFormattable` + `ISpanFormattable` + `IUtf8SpanFormattable` forwarding to `Value` (when applicable)
- Implicit/explicit conversion operators to/from `TValue`
- `INewable<TSelf>` / `INewable<TSelf, TValue>` implementation
- `New(TValue value)` static factory method
- `New()` (parameterless) for `Guid`- and `Ulid`-backed IDs, using `Guid.CreateVersion7()` on .NET 9+ or `Guid.NewGuid()` on earlier targets / `Ulid.NewUlid()`

## Factory Methods

```csharp
// Guid-backed: parameterless New() generates a new GUID
// On .NET 9+, uses Guid.CreateVersion7(); earlier targets use Guid.NewGuid()
var userId  = UserId.New();              // new UserId(Guid.CreateVersion7()) on .NET 9+
var userId2 = UserId.New(someGuid);      // new UserId(someGuid)

// Ulid-backed: parameterless New() generates a new ULID
var traceId = TraceId.New();             // new TraceId(Ulid.NewUlid())

// String-backed
var productId = ProductId.New("p-123");  // new ProductId("p-123")

// int-backed (no parameterless New())
var orderId = OrderId.New(42);           // new OrderId(42)
```

## EF Core Integration

Reference `Microsoft.EntityFrameworkCore` — no other configuration needed. The generator emits 
value converters and registers them via `UseStructId()`:

```csharp
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite("Data Source=app.db")
    .UseStructId()          // registers all struct ID value converters
    .Options;

// Or inside OnConfiguring:
protected override void OnConfiguring(DbContextOptionsBuilder builder) => builder.UseStructId();
```

**Value type coverage:**
- Built-in: `Guid`, `int`, `long`, `string`, `bool`, `byte`, `short`, `float`, `double`, `decimal`, `DateTime`, `DateTimeOffset`, `TimeSpan`
- Automatic via `IParsable<T>` + `IFormattable`: `Ulid` and any custom type implementing both
- Custom: any `ValueConverter<TModel, TProvider>` subclass in your project is auto-registered

## Dapper Integration

Reference `Dapper` — no other configuration needed. The generator emits `SqlMapper.TypeHandler<T>` 
implementations and registers them via `UseStructId()`:

```csharp
using var connection = new SqliteConnection("Data Source=app.db");
connection.UseStructId();   // registers all struct ID type handlers
connection.Open();
```

**Value type coverage:**
- Built-in: `Guid`, `int`, `long`, `string`
- Automatic via `IParsable<T>` + `IFormattable`: `Ulid` and any custom type implementing both
- Custom: any `SqlMapper.TypeHandler<T>` subclass in your project is auto-registered

## System.Text.Json Integration

Activated automatically when the value type implements `IParsable<T>`. Uses `JsonConverter<T>` 
that serializes/deserializes via the `Value` property string representation.

## Newtonsoft.Json Integration

Reference `Newtonsoft.Json` — the generator emits `JsonConverter<T>` subclasses automatically.

## Ulid Integration

Reference the `Ulid` NuGet package. Since `Ulid` implements `IParsable<T>` and `IFormattable`:
- EF Core and Dapper handlers are generated automatically
- A parameterless `New()` factory is generated using `Ulid.NewUlid()`

```csharp
public readonly partial record struct TraceId : IStructId<Ulid>;

var id = TraceId.New();   // new TraceId(Ulid.NewUlid())
```

## Custom Templates (`[TStructId]`)

The template system allows extending all (or a subset of) struct IDs with additional interfaces 
or members. Templates are regular C# files in your project.

### Template Rules

1. Must be annotated with `[TStructId]`
2. Must be `file partial record struct` (file-scoped to avoid polluting the assembly)
3. Must be named `TSelf`
4. Primary constructor parameter (if present) must be named `Value` — its type controls which 
   struct IDs the template applies to

### Template Examples

**Apply to all struct IDs (any value type):**

```csharp
using StructId;

[TStructId]
file partial record struct TSelf(TValue Value)
{
    public static implicit operator TValue(TSelf id) => id.Value;
    public static explicit operator TSelf(TValue value) => new(value);
}

file record struct TValue;  // empty = match any value type
```

**Apply only to string-backed IDs:**

```csharp
using StructId;

[TStructId]
file partial record struct TSelf(string Value)
{
    public static implicit operator string(TSelf id) => id.Value;
    public static explicit operator TSelf(string value) => new(value);
}
```

**Apply only to Guid-backed IDs:**

```csharp
using StructId;

[TStructId]
file partial record struct TSelf(Guid Value) : IMyGuidId
{
    public Guid AsGuid() => Value;
}
```

**Apply to IDs whose value type implements a specific interface:**

```csharp
using StructId;

[TStructId]
file partial record struct TSelf(TValue Value) : IComparable<TSelf>
{
    public int CompareTo(TSelf other) => ((IComparable<TValue>)Value).CompareTo(other.Value);

    public static bool operator <(TSelf left, TSelf right) => left.Value.CompareTo(right.Value) < 0;
    public static bool operator <=(TSelf left, TSelf right) => left.Value.CompareTo(right.Value) <= 0;
    public static bool operator >(TSelf left, TSelf right) => left.Value.CompareTo(right.Value) > 0;
    public static bool operator >=(TSelf left, TSelf right) => left.Value.CompareTo(right.Value) >= 0;
}

// Constrain TValue — only applies to IDs whose value type implements IComparable<TValue>
file record struct TValue : IComparable<TValue>
{
    public int CompareTo(TValue other) => throw new NotImplementedException();
}
```

**Exclude string from TValue matching:**

```csharp
using StructId;

// /*!string*/ inline comment excludes string-backed IDs
[TStructId]
file partial record struct TSelf(/*!string*/ TValue Value)
{
    // only applies to non-string value types
}

file record struct TValue;
```

**Add TSelf interface constraint (additional filtering):**

```csharp
using StructId;

[TStructId]
file partial record struct TSelf(Ulid Value)
{
    public static TSelf New() => new(Ulid.NewUlid());
}

// This partial declaration is removed at expansion time; it only constrains matching
file partial record struct TSelf : INewable<TSelf, Ulid>
{
    public static TSelf New(Ulid value) => throw new NotImplementedException();
}
```

### What Happens at Expansion Time

For a struct ID `PersonId : IStructId<Guid>` and a template applying to `Guid`-backed IDs:

1. `[TStructId]` attribute is removed from the output
2. `TSelf` is replaced with `PersonId`
3. `TValue` is replaced with `Guid`
4. The primary constructor is removed (provided by `ConstructorGenerator`)
5. The `file` modifier is removed from the type declaration
6. The output is wrapped in the same namespace as `PersonId`
7. File-local helper types (like `file record struct TValue`) are removed from output

### `TValue` Prefixed Identifiers

To generate unique helper type names per struct ID, use the `TSelf_` or `TValue_` prefix:

```csharp
using StructId;

[TStructId]
file partial record struct TSelf(TValue Value)
{
    // TSelf_Helper becomes PersonId_Helper, OrderId_Helper, etc.
    private sealed class TSelf_Helper { }
}

file record struct TValue;
```

## Custom Value-Type Templates (`[TValue]`)

For custom Dapper handlers or EF Core converters for a specific value type, use `[TValue]`:

```csharp
using StructId;

[TValue]
file class TValue_Handler : SqlMapper.TypeHandler<TValue>
{
    public override void SetValue(IDbDataParameter parameter, TValue value)
        => parameter.Value = value.ToString();

    public override TValue Parse(object value)
        => TValue.Parse((string)value, null);
}

file record struct TValue : IParsable<TValue>, IFormattable
{
    // Define the value type constraints
}
```

These are automatically discovered and registered in the generated `UseStructId` extension.

## Diagnostics and Code Fixes

| Diagnostic | Trigger | Auto-Fix Available |
|---|---|---|
| SID001 | Struct ID is not `readonly partial record struct` | ✅ Add missing modifiers |
| SID002 | Primary constructor parameter not named `Value` (or multiple params) | ✅ Rename to `Value` / Remove constructor |
| SID003 | `[TStructId]` type is not `file partial record struct` | ✅ Add `file` modifier |
| SID004 | `[TStructId]` constructor parameter not named `Value` | ✅ Rename to `Value` |
| SID005 | `[TStructId]` type is not named `TSelf` | ✅ Rename type |

## Installation

```xml
<PackageReference Include="StructId" Version="*" />
```

- Install only in the **top-level project** — analyzers and generators propagate transitively to all referencing projects
- The package is `developmentDependency="true"` — no runtime dependency is added to consumers

## Integration Auto-Activation

Features activate automatically when the corresponding package is referenced:

| Package | Generated Feature |
|---|---|
| `Microsoft.EntityFrameworkCore` | `ValueConverter<T, TProvider>` + `UseStructId(DbContextOptionsBuilder)` |
| `Dapper` | `SqlMapper.TypeHandler<T>` + `UseStructId(IDbConnection)` |
| `Newtonsoft.Json` | `JsonConverter<T>` subclass |
| `Ulid` | `Ulid`-specific handlers + parameterless `New()` factory |

No attribute, configuration, or code change is needed — just add the NuGet reference and rebuild.

## Common Patterns

### Entity with typed ID in EF Core

```csharp
using StructId;

public readonly partial record struct UserId : IStructId<Guid>;

public class User
{
    public UserId Id { get; set; } = UserId.New();
    public string Name { get; set; } = "";
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<User>().HasKey(u => u.Id);
    }
}

// Setup
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite("Data Source=app.db")
    .UseStructId()
    .Options;
```

### Dapper query with struct ID

```csharp
using StructId;

public readonly partial record struct ProductId : IStructId<int>;

using var connection = new SqliteConnection("Data Source=app.db");
connection.UseStructId();
connection.Open();

var product = connection.QueryFirst<Product>(
    "SELECT * FROM Products WHERE Id = @Id",
    new { Id = new ProductId(42) });
```

### Generic repository using INewable

```csharp
using StructId;

public class Repository<TEntity, TId, TValue>
    where TId : struct, IStructId<TValue>, INewable<TId, TValue>
    where TValue : struct
{
    public TEntity GetById(TValue rawValue) => Get(TId.New(rawValue));
    private TEntity Get(TId id) => /* ... */;
}
```

### Custom template for domain-specific interface

```csharp
// IEntityId.cs — custom interface
public interface IEntityId
{
    Guid AsGuid();
}

// EntityIdTemplate.cs — template to implement it for all Guid-backed IDs
[TStructId]
file partial record struct TSelf(Guid Value) : IEntityId
{
    public Guid AsGuid() => Value;
}
```

## Conventions

- Struct IDs must be `readonly partial record struct`
- Always use `IStructId<TValue>` for struct value types; use `IStructId` for strings
- Templates must be in `file partial record struct TSelf` named files; no specific file naming required
- `TValue` placeholder in templates means "any value type"; add interfaces to constrain it
- Use `TSelf.New()` (parameterless) for `Guid` and `Ulid` IDs; `TSelf.New(value)` for all others
- `UseStructId()` must be called once at startup for EF Core (`DbContextOptionsBuilder`) and Dapper (`IDbConnection`)
- Custom `ValueConverter<,>` and `SqlMapper.TypeHandler<T>` subclasses in the project are auto-registered
