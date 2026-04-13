using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace StructId;

/// <summary>
/// A wrapper around <see cref="ImmutableArray{T}"/> that provides value equality 
/// (sequence comparison) for use in incremental generator pipeline values.
/// </summary>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    readonly ImmutableArray<T> array;

    public EquatableArray(ImmutableArray<T> array) => this.array = array;

    public ImmutableArray<T> AsImmutableArray() => array.IsDefault ? ImmutableArray<T>.Empty : array;

    public int Length => array.IsDefault ? 0 : array.Length;

    public T this[int index] => array[index];

    public bool Contains(T item) => !array.IsDefault && array.Contains(item);

    public bool Equals(EquatableArray<T> other)
    {
        var a = array.IsDefault ? ImmutableArray<T>.Empty : array;
        var b = other.array.IsDefault ? ImmutableArray<T>.Empty : other.array;

        if (a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i]))
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj)
        => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (array.IsDefault || array.Length == 0)
            return 0;

        unchecked
        {
            int hash = 17;
            foreach (var item in array)
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public ImmutableArray<T>.Enumerator GetEnumerator()
        => array.IsDefault ? ImmutableArray<T>.Empty.GetEnumerator() : array.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
        => ((IEnumerable<T>)(array.IsDefault ? ImmutableArray<T>.Empty : array)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable)(array.IsDefault ? ImmutableArray<T>.Empty : array)).GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
        => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
        => !left.Equals(right);

    public static implicit operator EquatableArray<T>(ImmutableArray<T> array) => new(array);

    public static implicit operator ImmutableArray<T>(EquatableArray<T> array) => array.AsImmutableArray();
}
