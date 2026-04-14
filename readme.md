![Icon](assets/img/icon-32.png) StructId
============

[![Version](https://img.shields.io/nuget/vpre/StructId.svg?color=royalblue)](https://www.nuget.org/packages/StructId)
[![Downloads](https://img.shields.io/nuget/dt/StructId.svg?color=darkmagenta)](https://www.nuget.org/packages/StructId)
[![EULA](https://img.shields.io/badge/EULA-OSMF-blue?labelColor=black&color=C9FF30)](https://github.com/devlooped/oss/blob/main/osmfeula.txt)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/devlooped/oss/blob/main/license.txt)
[![Build](https://github.com/devlooped/StructId/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/devlooped/StructId/actions)

<!-- #content -->
An opinionated strongly-typed ID library that uses `readonly record struct` in C# for 
maximum performance, minimal memory allocation typed identifiers.

```csharp
public readonly partial record struct UserId : IStructId<Guid>;
// String-based ID
public readonly partial record struct ProductId : IStructId;
```

Unlike other such libraries for .NET, StructId introduces several unique features:

1. **Zero** run-time dependencies: everything is source-generated in your project.
1. **Zero** configuration: additional features are automatically added as you reference 
   dependencies that require them. For example: if your project references EF Core, 
   Dapper, or Newtonsoft.Json, the corresponding serialization and deserialization 
   code will be emitted without any additional configuration for the generation itself.
1. Leverages newest language and runtime features for cleaner and more efficient code, 
   such as:
   * `IParsable<T>`/`ISpanParsable<T>` for parsing from strings.
   * Static interface members, for consistent `TSelf.New(TValue value)` factory 
      method and proper type constraint (via a provided `INewable<TSelf, TValue>` interface).
   * File-scoped compiled C# templates for unparalleled authoring and extensibility experience.

## Usage

After installing the [StructId package](https://nuget.org/packages/StructId), the project 
(with a direct reference to the `StructId` package) will contain the main interfaces 
`IStruct` (for string-typed IDs) and `IStructId<TValue>`. 

> NOTE: the package only needs to be installed in the top-level project in your solution, 
> since analyzers/generators will [automatically propagate to referencing projects]((https://github.com/dotnet/sdk/issues/1212)).

The package is a [development dependency](https://github.com/NuGet/Home/wiki/DevelopmentDependency-support-for-PackageReference), 
meaning it will not add any run-time dependencies to your project (or package if you 
publish one that uses struct ids).

You can simply declare a new ID type by implementing `IStructId<TValue>`:

```csharp
public readonly partial record struct UserId : IStructId<Guid>;
```

If the declaration is missing `partial`, `readonly` or `record struct`, a codefix will
be offered to correct it.

![codefix](https://raw.githubusercontent.com/devlooped/StructId/main/assets/img/record-codefix.png)

The relevant constructor and `Value` property will be generated for you, as well as 
as a few other common interfaces, such as `IComparable<T>`, `IParsable<TSelf>`, etc.

If you want to customize the primary constructor (i.e. to add custom attributes), 
you can provide it yourself too:

```csharp
public readonly partial record struct ProductId(int Value) : IStructId<int>;
```

It must contain a single parameter named `Value` (and codefixes will offer to rename or 
remove it if you don't need it anymore).

## EF Core

If you are using EF Core, the package will automatically generate the necessary value converters, 
as well as an `UseStructId` extension method for `DbContextOptionsBuilder` to set them up:

```csharp
var options = new DbContextOptionsBuilder<Context>()
    .UseSqlite("Data Source=ef.db")
    .UseStructId()
    .Options;

using var context = new Context(options);
// access your entities using struct ids
```

Alternatively, you can also invoke that method in the `OnConfiguring` method of your context:
```csharp
protected override void OnConfiguring(DbContextOptionsBuilder builder) => builder.UseStructId();
```

In addition to the natively supported primitive types, any other types that implement `IParsable<T>` 
and `IFormattable` get automatic support by persisting as strings. This means that you can, 
for example, use [Ulid](https://github.com/Cysharp/Ulid) out of the box without any further 
configuration or customization (since it implements both interfaces).

Additionally, the `UseStructId` will pick up any custom [ValueConverter<TModel,TProvider>](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.storage.valueconversion.valueconverter-2?view=efcore-9.0) 
you may add to your project and register them too for convenience.

## Dapper

If you are using Dapper, the package will automatically generate required `SqlMapper.TypeHandler<T>` 
for your ID types. The `UseStructId` extension method for `IDbConnection` can be used to register 
them as needed:

```csharp
using var connection = new SqliteConnection("Data Source=sqlite.db")

connection.UseStructId();
connection.Open();
```

The value types `Guid`, `int`, `long` and `string` have built-in support, as well as 
any other types that implement `IParsable<T>` and `IFormattable` (by persisting them 
as strings). This means that you can, for example, use [Ulid](https://github.com/Cysharp/Ulid) 
out of the box without any further configuration or customization (since it implements 
both interfaces).

Additionally, the `UseStructId` will pick up any custom `Dapper.SqlMapper.TypeHandler<T>`
you may add to your project and register them too for convenience.

## Ulid

Since [Ulid](https://github.com/Cysharp/Ulid) implements `IParsable<T>` and `IFormattable`,
it is supported out of the box without any further configuration or customization with both 
Dapper and EF Core.

In addition to the necessary converter/handler registration, the package will also generate 
a `New()` (parameterless) factory method for struct ids using `Ulid` as the value type, which 
in turn will use `Ulid.NewUlid()` to generate a new value. This mirrors the behavior 
generated for `Guid`-based struct ids.

```csharp
public readonly partial record struct ProductId : IStructId<Ulid>;

public record Product(ProductId Id, string Name);

// Create a new product with a new Ulid-based id
var productId = ProductId.New(); // 👈 equivalent to: new ProductId(Ulid.NewUlid())
var product = new Product(productId, "Product");

// Seed data
connection.Execute("INSERT INTO Products (Id, Name) VALUES (@Id, @Name)", new Product(ProductId.New(), "Product1"));
connection.Execute("INSERT INTO Products (Id, Name) VALUES (@Id, @Name)", product);
connection.Execute("INSERT INTO Products (Id, Name) VALUES (@Id, @Name)", new Product(ProductId.New(), "Product2"));

// showcase we can query by the underlying ulid
var saved1 = connection.QueryFirst<Product>("SELECT * FROM Products WHERE Id = @Id", new { Id = productId });

// showcase we can query by the ulid-based struct id
var saved2 = connection.QueryFirst<Product>("SELECT * FROM Products WHERE Id = @Id", new { Id = new ProductId(productId) });
```

## Customization via Templates

Virtually all the built-in interfaces and implementations are generated using the same compiled 
templates mechanism available to you. Templates are regular C# files in your project with a 
few constraints. Here's an example from the built-in ones:

```csharp
using System;
using StructId;

[TStructId]
file partial record struct TSelf(IUtf8SpanFormattable Value) : IUtf8SpanFormattable
{
    /// <inheritdoc/>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) 
        => ((IUtf8SpanFormattable)Value).TryFormat(utf8Destination, out bytesWritten, format, provider);
}
```

This type is considered a template because it's marked with the `[TStructId]` attribute. 
This introduces some restrictions that are enfored by an analyzer:
1. The type must be a `partial record struct` since it will complement a partial declaration 
   of that type by the user (i.e. `partial record struct PersonId : IStructId<Guid>;`)
1. The type must be [file-scoped](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/file), 
   which automatically prevents polluting your assembly with types that aren't intended for 
   direct consumption outside the template file itself.
1. The template can optionally declare the type of ID value it supports by introducing the 
   primary constructor with a single parameter named `Value` of that type.
1. The record itself must be named `TSelf`.

The template itself can introduce arbitrary code that will be emitted for each matching 
struct id (i.e. all struct ids whose value type implements `IUtf8SpanFormattable` in this case). 
In this example, the template simply offers a pass-through implementation of the `IUtf8SpanFormattable` 
value.

As another example, imagine you have some standardized way of treating IDs in your application, 
by providing an interface for them, which applies to all `Guid`-based IDs:

```csharp
public interface IId
{
    public Guid Id { get; }
}
```

You can now create a template that will automatically provide this interface for all struct 
ids that use `Guid` as their value type as follows:

```csharp
[TStructId]
file partial record struct TSelf(Guid Value) : IId
{
    public Guid Id => Value;
}
```

Another example of a built-in template that applies to a single type of `TValue` is 
the following:

```csharp
using StructId;

[TStructId]
file partial record struct TSelf(string Value)
{
    public static implicit operator string(TSelf id) => id.Value;
    public static explicit operator TSelf(string value) => new(value);
}
```

This template is a proper C# compilation unit, so you can use any C# feature that 
your project supports, since its output will also be emitted via a source generator 
in the same project for matching struct ids.

In the case of a struct id defined as follows:

```csharp
public partial record struct PersonId : IStructId<Guid>;
```

The template will be applied automatically and result in a partial declaration 
like:

```csharp
partial record struct PersonId : IId
{
    public Guid Id => Value;
}
```

Things to note at template expansion time:
1. The `[TStructId]` attribute is removed from the generated type automatically.
2. The `TSelf` type is replaced with the actual name of the struct id.
3. The primary constructor on the template is removed since it is already provided 
   by another generator.

You can also constrain the type of `TValue` the template applies to by using using 
the special name `TValue` for the primary constructor parameter type, as in the following 
example from the implicit conversion template:

```csharp
using StructId;

[TStructId]
file partial record struct TSelf(TValue Value)
{
    public static implicit operator TValue(TSelf id) => id.Value;
    public static explicit operator TSelf(TValue value) => new(value);
}

// This will be removed when applying the template to each user-defined struct id.
file record struct TValue;
```

The `TValue` is subsequently defined as a file-local type where you can 
specify any interfaces it implements. If no constraints need to apply to 
`TValue`, you can just leave the declaration empty, meaning "any value type".

> NOTE: The type of declaration (struct, class, record, etc.) of `TValue` is not checked, 
> since in many cases you'd end up having to create two versions of the same template, 
> one for structs and another for strings, since they are not value types and have no 
> common declaration type.

Here's another example from the built-in templates that uses this technique to
apply to all struct ids whose `TValue` implements `IComparable<TValue>`:

```csharp
using System;
using StructId;

[TStructId]
file partial record struct TSelf(TValue Value) : IComparable<TSelf>
{
    /// <inheritdoc/>
    public int CompareTo(TSelf other) => ((IComparable<TValue>)Value).CompareTo(other.Value);

    /// <inheritdoc/>
    public static bool operator <(TSelf left, TSelf right) => left.Value.CompareTo(right.Value) < 0;

    /// <inheritdoc/>
    public static bool operator <=(TSelf left, TSelf right) => left.Value.CompareTo(right.Value) <= 0;

    /// <inheritdoc/>
    public static bool operator >(TSelf left, TSelf right) => left.Value.CompareTo(right.Value) > 0;

    /// <inheritdoc/>
    public static bool operator >=(TSelf left, TSelf right) => left.Value.CompareTo(right.Value) >= 0;
}

// This will be removed when applying the template to each user-defined struct id.
file record struct TValue : IComparable<TValue>
{
    public int CompareTo(TValue other) => throw new NotImplementedException();
}
```

This automatically covers not only all built-in value types, but also any custom 
types that implement the interface, making the code generation much more flexible 
and powerful.

> NOTE: if you need to exclude just the string type from applying to the `TValue`, 
> you can use the inline comment `/*!string*/` in the primary constructor parameter 
> type, as in `TSelf(/*!string*/ TValue Value)`.

In addition to constraining on the `TValue` type, you can also constrain on the
the struct id/`TSelf` itself by declaring the inheritance requirements in a partial 
class of `TSelf` in the template. For example, the following (built-in) template 
ensures it's only applied to struct ids whose `TValue` is [Ulid](https://github.com/Cysharp/Ulid) 
and implement `INewable<TSelf, Ulid>`. This is useful in this case since the given 
interface constraint allows us to use the `TSelf.New(Ulid)` static interface 
factory method and have it recognized by the C# compiler as valid code as part of the 
implementation of introduced parameterless `New()` factory method provided by the template:

```csharp
[TStructId]
file partial record struct TSelf(Ulid Value)
{
    public static TSelf New() => new(Ulid.NewUlid());
}

// This will be removed when applying the template to each user-defined struct id.
file partial record struct TSelf : INewable<TSelf, Ulid>
{
    public static TSelf New(Ulid value) => throw new NotImplementedException();
}
```

> NOTE: the built-in templates will always emit an implementation of 
> `INewable<TSelf, TValue>` for all struct ids.
 
Here you can see that the constraint that the value type must be `Ulid` is enforced by 
the `TValue` constructor parameter type, while the interface constraint in the partial 
declaration enforces inheritance from `INewable<TSelf, Ulid>`. Since this part of 
the partial declaration is removed, there is no need to provide an actual implementation 
for the constrain interface(s), just the signature is enough. But the partial declaration
providing the interface constraint is necessary for the C# compiler to recognize the 
line with `public static TSelf New() => new(Ulid.NewUlid());` as valid.

<!-- #content -->
<!-- #ci -->

# Dogfooding

[![CI Version](https://img.shields.io/endpoint?url=https://shields.kzu.app/vpre/StructId/main&label=nuget.ci&color=brightgreen)](https://pkg.kzu.app/index.json)
[![Build](https://github.com/devlooped/StructId/workflows/build/badge.svg?branch=main)](https://github.com/devlooped/StructId/actions)

We also produce CI packages from branches and pull requests so you can dogfood builds as quickly as they are produced. 

The CI feed is `https://pkg.kzu.app/index.json`. 

The versioning scheme for packages is:

- PR builds: *42.42.42-pr*`[NUMBER]`
- Branch builds: *42.42.42-*`[BRANCH]`.`[COMMITS]`

<!-- include https://github.com/devlooped/.github/raw/main/osmf.md -->
## Open Source Maintenance Fee

To ensure the long-term sustainability of this project, users of this package who generate 
revenue must pay an [Open Source Maintenance Fee](https://opensourcemaintenancefee.org). 
While the source code is freely available under the terms of the [License](license.txt), 
this package and other aspects of the project require [adherence to the Maintenance Fee](osmfeula.txt).

To pay the Maintenance Fee, [become a Sponsor](https://github.com/sponsors/devlooped) at the proper 
OSMF tier. A single fee covers all of [Devlooped packages](https://www.nuget.org/profiles/Devlooped).

<!-- https://github.com/devlooped/.github/raw/main/osmf.md -->

<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->
# Sponsors 

<!-- sponsors.md -->
[![Clarius Org](https://avatars.githubusercontent.com/u/71888636?v=4&s=39 "Clarius Org")](https://github.com/clarius)
[![MFB Technologies, Inc.](https://avatars.githubusercontent.com/u/87181630?v=4&s=39 "MFB Technologies, Inc.")](https://github.com/MFB-Technologies-Inc)
[![Khamza Davletov](https://avatars.githubusercontent.com/u/13615108?u=11b0038e255cdf9d1940fbb9ae9d1d57115697ab&v=4&s=39 "Khamza Davletov")](https://github.com/khamza85)
[![SandRock](https://avatars.githubusercontent.com/u/321868?u=99e50a714276c43ae820632f1da88cb71632ec97&v=4&s=39 "SandRock")](https://github.com/sandrock)
[![DRIVE.NET, Inc.](https://avatars.githubusercontent.com/u/15047123?v=4&s=39 "DRIVE.NET, Inc.")](https://github.com/drivenet)
[![Keith Pickford](https://avatars.githubusercontent.com/u/16598898?u=64416b80caf7092a885f60bb31612270bffc9598&v=4&s=39 "Keith Pickford")](https://github.com/Keflon)
[![Thomas Bolon](https://avatars.githubusercontent.com/u/127185?u=7f50babfc888675e37feb80851a4e9708f573386&v=4&s=39 "Thomas Bolon")](https://github.com/tbolon)
[![Kori Francis](https://avatars.githubusercontent.com/u/67574?u=3991fb983e1c399edf39aebc00a9f9cd425703bd&v=4&s=39 "Kori Francis")](https://github.com/kfrancis)
[![Reuben Swartz](https://avatars.githubusercontent.com/u/724704?u=2076fe336f9f6ad678009f1595cbea434b0c5a41&v=4&s=39 "Reuben Swartz")](https://github.com/rbnswartz)
[![Jacob Foshee](https://avatars.githubusercontent.com/u/480334?v=4&s=39 "Jacob Foshee")](https://github.com/jfoshee)
[![](https://avatars.githubusercontent.com/u/33566379?u=bf62e2b46435a267fa246a64537870fd2449410f&v=4&s=39 "")](https://github.com/Mrxx99)
[![Eric Johnson](https://avatars.githubusercontent.com/u/26369281?u=41b560c2bc493149b32d384b960e0948c78767ab&v=4&s=39 "Eric Johnson")](https://github.com/eajhnsn1)
[![Jonathan ](https://avatars.githubusercontent.com/u/5510103?u=98dcfbef3f32de629d30f1f418a095bf09e14891&v=4&s=39 "Jonathan ")](https://github.com/Jonathan-Hickey)
[![Ken Bonny](https://avatars.githubusercontent.com/u/6417376?u=569af445b6f387917029ffb5129e9cf9f6f68421&v=4&s=39 "Ken Bonny")](https://github.com/KenBonny)
[![Simon Cropp](https://avatars.githubusercontent.com/u/122666?v=4&s=39 "Simon Cropp")](https://github.com/SimonCropp)
[![agileworks-eu](https://avatars.githubusercontent.com/u/5989304?v=4&s=39 "agileworks-eu")](https://github.com/agileworks-eu)
[![Zheyu Shen](https://avatars.githubusercontent.com/u/4067473?v=4&s=39 "Zheyu Shen")](https://github.com/arsdragonfly)
[![Vezel](https://avatars.githubusercontent.com/u/87844133?v=4&s=39 "Vezel")](https://github.com/vezel-dev)
[![ChilliCream](https://avatars.githubusercontent.com/u/16239022?v=4&s=39 "ChilliCream")](https://github.com/ChilliCream)
[![4OTC](https://avatars.githubusercontent.com/u/68428092?v=4&s=39 "4OTC")](https://github.com/4OTC)
[![domischell](https://avatars.githubusercontent.com/u/66068846?u=0a5c5e2e7d90f15ea657bc660f175605935c5bea&v=4&s=39 "domischell")](https://github.com/DominicSchell)
[![Adrian Alonso](https://avatars.githubusercontent.com/u/2027083?u=129cf516d99f5cb2fd0f4a0787a069f3446b7522&v=4&s=39 "Adrian Alonso")](https://github.com/adalon)
[![torutek](https://avatars.githubusercontent.com/u/33917059?v=4&s=39 "torutek")](https://github.com/torutek)
[![mccaffers](https://avatars.githubusercontent.com/u/16667079?u=110034edf51097a5ee82cb6a94ae5483568e3469&v=4&s=39 "mccaffers")](https://github.com/mccaffers)
[![Seika Logiciel](https://avatars.githubusercontent.com/u/2564602?v=4&s=39 "Seika Logiciel")](https://github.com/SeikaLogiciel)
[![Andrew Grant](https://avatars.githubusercontent.com/devlooped-user?s=39 "Andrew Grant")](https://github.com/wizardness)
[![Lars](https://avatars.githubusercontent.com/u/1727124?v=4&s=39 "Lars")](https://github.com/latonz)
[![prime167](https://avatars.githubusercontent.com/u/3722845?v=4&s=39 "prime167")](https://github.com/prime167)


<!-- sponsors.md -->
[![Sponsor this project](https://avatars.githubusercontent.com/devlooped-sponsor?s=118 "Sponsor this project")](https://github.com/sponsors/devlooped)

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- https://github.com/devlooped/sponsors/raw/main/footer.md -->
