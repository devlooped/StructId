﻿// <auto-generated/>
#nullable enable

using System.Diagnostics.CodeAnalysis;
using System;
using StructId;

[TStructId]
file readonly partial record struct TSelf(/*!string*/ TValue Value) : IParsable<TSelf>
{
    /// <inheritdoc cref="IParsable{TSelf}"/>
    public static TSelf Parse(string s, IFormatProvider? provider) => new(TValue.Parse(s, provider));

    /// <inheritdoc cref="IParsable{TSelf}"/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TSelf result)
    {
        if (TValue.TryParse(s, provider, out var value))
        {
            result = new TSelf(value);
            return true;
        }
        result = default;
        return false;
    }
}

file record struct TValue : IParsable<TValue>
{
    public static TValue Parse(string s, IFormatProvider? provider) => throw new NotImplementedException();
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TValue result) => throw new NotImplementedException();
}