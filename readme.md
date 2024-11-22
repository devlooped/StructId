![Icon](img/icon-32.png) ThisAssembly
============

[![Version](https://img.shields.io/nuget/vpre/ThisAssembly.svg?color=royalblue)](https://www.nuget.org/packages/ThisAssembly)
[![Downloads](https://img.shields.io/nuget/dt/ThisAssembly.svg?color=green)](https://www.nuget.org/packages/ThisAssembly)
[![License](https://img.shields.io/github/license/devlooped/ThisAssembly.svg?color=blue)](https://github.com//devlooped/ThisAssembly/blob/main/license.txt)
[![Build](https://github.com/devlooped/ThisAssembly/workflows/build/badge.svg?branch=main)](https://github.com/devlooped/ThisAssembly/actions)

<!-- #content -->
An opinionated strongly-typed ID library that uses `readonly record struct` in C# for 
maximum performance, minimal memory allocation typed identifiers.

```csharp
public readonly partial record struct UserId : IStructId<Guid>;
```
<!-- #content -->
<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->