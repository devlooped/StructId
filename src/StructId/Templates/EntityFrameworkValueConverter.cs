﻿// <auto-generated />
#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StructId;

[TValue]
file class TValue_ValueConverter : ValueConverter<TValue, string>
{
    public TValue_ValueConverter() : this(null) { }

    public TValue_ValueConverter(ConverterMappingHints? mappingHints = null)
        : base(id => id.ToString(null, null), value => TValue.Parse(value, null), mappingHints) { }
}

// This will be removed when applying the template to each user-defined struct id.
file partial struct TValue : IParsable<TValue>, IFormattable
{
    public static TValue Parse(string s, IFormatProvider? provider) => throw new NotImplementedException();
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TValue result) => throw new NotImplementedException();
    public string ToString(string? format, IFormatProvider? formatProvider) => throw new NotImplementedException();
}