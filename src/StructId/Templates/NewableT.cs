﻿// <auto-generated />

using StructId;

[TStructId]
file partial record struct TSelf(TValue Value) : INewable<TSelf, TValue>
{
    /// <inheritdoc/>
    public static TSelf New(TValue value) => new(value);
}

// This will be removed when applying the template to each user-defined struct id.
file record struct TValue;