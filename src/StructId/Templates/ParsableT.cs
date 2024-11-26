﻿// <auto-generated/>
#nullable enable

using System.Diagnostics.CodeAnalysis;
using System;

readonly partial record struct TSelf : IParsable<TSelf>
{
    public static TSelf Parse(string s, IFormatProvider? provider) => new(TId.Parse(s, provider));

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TSelf result)
    {
        if (TId.TryParse(s, provider, out var value))
        {
            result = new TSelf(value);
            return true;
        }
        result = default;
        return false;
    }
}