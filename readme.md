![Icon](assets/img/icon-32.png) StrucTId
============

[![Version](https://img.shields.io/nuget/vpre/StructId.svg?color=royalblue)](https://www.nuget.org/packages/StructId)
[![Downloads](https://img.shields.io/nuget/dt/StructId.svg?color=green)](https://www.nuget.org/packages/StructId)
[![License](https://img.shields.io/github/license/devlooped/StructId.svg?color=blue)](https://github.com//devlooped/StructId/blob/main/license.txt)
[![Build](https://github.com/devlooped/StructId/workflows/build/badge.svg?branch=main)](https://github.com/devlooped/StructId/actions)

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
   code will be emitted without any additional configuration.
1. Leverages newest language and runtime features for cleaner and more efficient code, 
   such as:
   1. IParsable<T> for parsing from strings.
   1. Static interface members, for consistent `TSelf.New(TId value)` factory 
      method and proper type constraint (via a provided `INewable<TSelf, TId>` interface).


## Usage

After installing the [StructId package](https://nuget.org/packages/StructId), the project 
(with a direct reference to the `StructId` package) will contain the main interfaces 
`IStruct` (for string-typed IDs) and `IStructId<TId>`. 

> NOTE: the package only needs to be installed in the top-level project in your solution, 
> since analyzers/generators will [automatically propagate to referencing projects]((https://github.com/dotnet/sdk/issues/1212)).

The package is a [development dependency](https://github.com/NuGet/Home/wiki/DevelopmentDependency-support-for-PackageReference), 
meaning it will not contribute to any run-time dependencies of your project (or package if 
you publish one).

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

### EF Core

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
