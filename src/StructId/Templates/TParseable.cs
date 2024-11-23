﻿// <auto-generated/>
#nullable enable

using System.Diagnostics.CodeAnalysis;
using System;

readonly partial record struct TStruct : IParsable<TStruct>
{
    public static TStruct Parse(string s, IFormatProvider? provider) => new(TValue.Parse(s));

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TStruct result)
    {
        if (TValue.TryParse(s, out var value))
        {
            result = new TStruct(value);
            return true;
        }
        result = default;
        return false;
    }
}