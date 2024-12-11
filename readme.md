![Icon](assets/img/icon-32.png) StructId
============

[![Version](https://img.shields.io/nuget/vpre/StructId.svg?color=royalblue)](https://www.nuget.org/packages/StructId)
[![Downloads](https://img.shields.io/nuget/dt/StructId.svg?color=green)](https://www.nuget.org/packages/StructId)
[![License](https://img.shields.io/github/license/devlooped/StructId.svg?color=blue)](https://github.com//devlooped/StructId/blob/main/license.txt)
[![Build](https://github.com/devlooped/StructId/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/devlooped/StructId/actions)

<!-- #content -->
An opinionated strongly-typed ID library that uses `readonly record struct` in C# for 
maximum performance, minimal memory allocation typed identifiers.

```csharp
public readonly partial record struct UserId : IStructId<Guid>;
```

Unlike other such libraries for .NET, StructId introduces several unique features:

1. **Zero** run-time dependencies: everything is source-generated in your project.
1. **Zero** configuration: additional features are automatically added as you reference 
   dependencies that require them. For example: if your project references EF Core, 
   Dapper, or Newtonsoft.Json, the corresponding serialization and deserialization 
   code will be emitted without any additional configuration for the generation itself.
1. Leverages newest language and runtime features for cleaner and more efficient code, 
   such as:
   1. `IParsable<T>`/`ISpanParsable<T>` for parsing from strings.
   1. Static interface members, for consistent `TSelf.New(TId value)` factory 
      method and proper type constraint (via a provided `INewable<TSelf, TId>` interface).
   1. File-scoped C# templates for unparalelled authoring and extensibility experience.

## Usage

After installing the [StructId package](https://nuget.org/packages/StructId), the project 
(with a direct reference to the `StructId` package) will contain the main interfaces 
`IStruct` (for string-typed IDs) and `IStructId<TId>`. 

> NOTE: the package only needs to be installed in the top-level project in your solution, 
> since analyzers/generators will [automatically propagate to referencing projects]((https://github.com/dotnet/sdk/issues/1212)).

The package is a [development dependency](https://github.com/NuGet/Home/wiki/DevelopmentDependency-support-for-PackageReference), 
meaning it will not add any run-time dependencies to your project (or package if you 
publish one that uses struct ids).

The default target namespace for the included types will match the `RootNamespace` of the 
project, but can be customized by setting the `StructIdNamespace` property.

You can simply declare a new ID type by implementing `IStructId<TId>`:

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
1. The `TSelf` type is replaced with the actual name of the struct id.
1. The primary constructor on the template is removed since it is already provided 
   by anoother generator.


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

<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->
# Sponsors 

<!-- sponsors.md -->
[![Clarius Org](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/clarius.png "Clarius Org")](https://github.com/clarius)
[![Kirill Osenkov](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/KirillOsenkov.png "Kirill Osenkov")](https://github.com/KirillOsenkov)
[![MFB Technologies, Inc.](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/MFB-Technologies-Inc.png "MFB Technologies, Inc.")](https://github.com/MFB-Technologies-Inc)
[![Torutek](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/torutek-gh.png "Torutek")](https://github.com/torutek-gh)
[![DRIVE.NET, Inc.](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/drivenet.png "DRIVE.NET, Inc.")](https://github.com/drivenet)
[![Keith Pickford](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/Keflon.png "Keith Pickford")](https://github.com/Keflon)
[![Thomas Bolon](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/tbolon.png "Thomas Bolon")](https://github.com/tbolon)
[![Kori Francis](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/kfrancis.png "Kori Francis")](https://github.com/kfrancis)
[![Toni Wenzel](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/twenzel.png "Toni Wenzel")](https://github.com/twenzel)
[![Uno Platform](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/unoplatform.png "Uno Platform")](https://github.com/unoplatform)
[![Dan Siegel](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/dansiegel.png "Dan Siegel")](https://github.com/dansiegel)
[![Reuben Swartz](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/rbnswartz.png "Reuben Swartz")](https://github.com/rbnswartz)
[![Jacob Foshee](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/jfoshee.png "Jacob Foshee")](https://github.com/jfoshee)
[![](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/Mrxx99.png "")](https://github.com/Mrxx99)
[![Eric Johnson](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/eajhnsn1.png "Eric Johnson")](https://github.com/eajhnsn1)
[![Ix Technologies B.V.](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/IxTechnologies.png "Ix Technologies B.V.")](https://github.com/IxTechnologies)
[![David JENNI](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/davidjenni.png "David JENNI")](https://github.com/davidjenni)
[![Jonathan ](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/Jonathan-Hickey.png "Jonathan ")](https://github.com/Jonathan-Hickey)
[![Charley Wu](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/akunzai.png "Charley Wu")](https://github.com/akunzai)
[![Jakob Tikjøb Andersen](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/jakobt.png "Jakob Tikjøb Andersen")](https://github.com/jakobt)
[![Tino Hager](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/tinohager.png "Tino Hager")](https://github.com/tinohager)
[![Mark Seemann](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/ploeh.png "Mark Seemann")](https://github.com/ploeh)
[![Ken Bonny](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/KenBonny.png "Ken Bonny")](https://github.com/KenBonny)
[![Simon Cropp](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/SimonCropp.png "Simon Cropp")](https://github.com/SimonCropp)
[![agileworks-eu](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/agileworks-eu.png "agileworks-eu")](https://github.com/agileworks-eu)
[![sorahex](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/sorahex.png "sorahex")](https://github.com/sorahex)
[![Zheyu Shen](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/arsdragonfly.png "Zheyu Shen")](https://github.com/arsdragonfly)
[![Vezel](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/vezel-dev.png "Vezel")](https://github.com/vezel-dev)
[![ChilliCream](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/ChilliCream.png "ChilliCream")](https://github.com/ChilliCream)
[![4OTC](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/4OTC.png "4OTC")](https://github.com/4OTC)
[![Vincent Limo](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/v-limo.png "Vincent Limo")](https://github.com/v-limo)
[![Jordan S. Jones](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/jordansjones.png "Jordan S. Jones")](https://github.com/jordansjones)
[![domischell](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/DominicSchell.png "domischell")](https://github.com/DominicSchell)


<!-- sponsors.md -->

[![Sponsor this project](https://raw.githubusercontent.com/devlooped/sponsors/main/sponsor.png "Sponsor this project")](https://github.com/sponsors/devlooped)
&nbsp;

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- https://github.com/devlooped/sponsors/raw/main/footer.md -->
