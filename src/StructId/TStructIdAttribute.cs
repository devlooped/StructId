using System;

namespace StructId;

/// <summary>
/// Attribute for marking a template type for a struct id based on 
/// a generic type parameter, which would implement <see cref="IStructId{TId}"/>.
/// </summary>
/// <typeparam name="TId">Template for the TId to replace in the <see cref="IStructId{TId}"/.></typeparam>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class TStructIdAttribute<TId> : Attribute
{
}

/// <summary>
/// Attribute for marking a template type for a struct id based on a string value, 
/// which would implement <see cref="IStructId"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class TStructIdAttribute : Attribute
{
}