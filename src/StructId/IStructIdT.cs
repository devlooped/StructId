﻿// <auto-generated />

namespace StructId;

/// <summary>
/// Interface for struct-based identifiers.
/// </summary>
/// <typeparam name="T">The struct type for the inner <see cref="Value"/> of the identifier.</typeparam>
public partial interface IStructId<T> where T : struct
{
    /// <summary>
    /// Gets the underlying value of the identifier.
    /// </summary>
    T Value { get; }
}