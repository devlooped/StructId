﻿using System;
using System.Diagnostics.CodeAnalysis;
using StructId;

partial record struct TId : ISpanParsable<TId>, IComparable<TId>
{
    public static TId Parse(string s, IFormatProvider? provider)
        => s is null ? throw new ArgumentNullException(nameof(s)) : new TId();

    public static TId Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new();

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TId result)
    {
        if (s is not null)
        {
            result = new();
            return true;
        }
        result = default;
        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out TId result)
        => TryParse(s.ToString(), provider, out result);

    public int CompareTo(TId other) => throw new NotImplementedException();
}

readonly partial record struct Self : IStructId
{
}

readonly partial record struct TSelf : IStructId<TId>
{
    public TSelf(Guid _) : this(default(TId)) { }
}
