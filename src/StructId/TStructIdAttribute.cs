using System;

namespace StructId;

/// <summary>
/// Attribute for marking a template type for a struct id.
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class TStructIdAttribute : Attribute
{
}