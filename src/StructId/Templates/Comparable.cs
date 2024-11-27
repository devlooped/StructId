﻿// <auto-generated />

using System;

readonly partial record struct TSelf : IComparable<TSelf>
{
    /// <inheritdoc/>
    public int CompareTo(TSelf other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public static bool operator <(TSelf left, TSelf right) => left.Value.CompareTo(right.Value) < 0;

    /// <inheritdoc/>
    public static bool operator <=(TSelf left, TSelf right) => left.Value.CompareTo(right.Value) <= 0;

    /// <inheritdoc/>
    public static bool operator >(TSelf left, TSelf right) => left.Value.CompareTo(right.Value) > 0;

    /// <inheritdoc/>
    public static bool operator >=(TSelf left, TSelf right) => left.Value.CompareTo(right.Value) >= 0;
}