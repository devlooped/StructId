﻿// <auto-generated />
#nullable enable

using System.Diagnostics.CodeAnalysis;
using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StructId;

[TStructId]
file partial record struct TSelf(TValue Value) : INewable<TSelf, TValue>
{
    /// <summary>
    /// Provides value conversion for Entity Framework Core
    /// </summary>
    public partial class EntityFrameworkValueConverter : ValueConverter<TSelf, string>
    {
        public EntityFrameworkValueConverter() : this(null) { }

        public EntityFrameworkValueConverter(ConverterMappingHints? mappingHints = null)
        : base(id => id.Value.ToString(null, null), value => TSelf.New(TValue.Parse(value, null)), mappingHints) { }
    }
}

file partial record struct TSelf
{
    public static TSelf New(TValue value) => throw new System.NotImplementedException();
}

file partial struct TValue : IParsable<TValue>, IFormattable
{
    public static TValue Parse(string s, IFormatProvider? provider) => throw new NotImplementedException();
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TValue result) => throw new NotImplementedException();
    public string ToString(string? format, IFormatProvider? formatProvider) => throw new NotImplementedException();
}