using System;
using System.Diagnostics.CodeAnalysis;
using StructId;

partial record struct TId : IParsable<TId>, IComparable<TId>
{
    public static TId Parse(string s, IFormatProvider? provider)
        => s is null ? throw new ArgumentNullException(nameof(s)) : new TId();

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

    public int CompareTo(TId other) => other.CompareTo(this);
}

readonly partial record struct Self(string Value) : IStructId
{
}

readonly partial record struct TSelf(TId Value) : IStructId<TId>
{
    public TSelf(Guid _) : this(default(TId)) { }
}
