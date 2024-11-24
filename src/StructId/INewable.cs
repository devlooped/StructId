﻿// <auto-generated />

namespace StructId;

/// <summary>
/// Interface implemented by <see cref="IStructId"/> and <see cref="IStructId{T}"/> 
/// that support creating new instances of the identifier.
/// </summary>
public interface INewable<TSelf>
{
    /// <summary>
    /// Creates a new identifier from the given identifier value.
    /// </summary>
    public abstract static TSelf New(string value);
}