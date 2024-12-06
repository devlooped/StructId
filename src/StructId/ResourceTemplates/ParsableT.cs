﻿// <auto-generated/>
#nullable enable

using System.Diagnostics.CodeAnalysis;
using System;

readonly partial record struct TSelf : ISpanParsable<TSelf>
{
    /// <inheritdoc cref="IParsable{TSelf}"/>
    public static TSelf Parse(string s, IFormatProvider? provider) => new(TId.Parse(s, provider));

    /// <inheritdoc cref="IParsable{TSelf}"/>
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

    /// <inheritdoc cref="ISpanParsable{TSelf}"/>
    public static TSelf Parse(ReadOnlySpan<char> input, global::System.IFormatProvider? provider) => new(TId.Parse(input, provider));

    /// <inheritdoc cref="ISpanParsable{TSelf}"/>
    public static bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, out TSelf result)
    {
        if (TId.TryParse(input, provider, out var value))
        {
            result = new TSelf(value);
            return true;
        }
        result = default;
        return false;
    }
}