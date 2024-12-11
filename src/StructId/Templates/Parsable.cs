﻿// <auto-generated/>
#nullable enable

using System.Diagnostics.CodeAnalysis;
using System;
using StructId;

[TStructId]
file readonly partial record struct TSelf(string Value) : IParsable<TSelf>
{
    /// <inheritdoc cref="IParsable{TSelf}"/>
    public static TSelf Parse(string s, IFormatProvider? provider) => new(s ?? throw new ArgumentNullException(nameof(s)));

    /// <inheritdoc cref="IParsable{TSelf}"/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TSelf result)
    {
        if (s is not null)
        {
            result = new(s);
            return true;
        }
        result = default;
        return false;
    }
}