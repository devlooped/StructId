using System;
using System.Diagnostics.CodeAnalysis;
using StructId;

partial record struct TValue : IParsable<TValue>
{
    public static TValue Parse(string s, IFormatProvider? provider)
        => s is null ? throw new ArgumentNullException(nameof(s)) : new TValue();

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TValue result)
    {
        if (s is not null)
        {
            result = new();
            return true;
        }
        result = default;
        return false;
    }
}

readonly partial record struct SStruct(string Value) : IStructId
{
}

readonly partial record struct TStruct(TValue Value) : IStructId<TValue>
{
}
