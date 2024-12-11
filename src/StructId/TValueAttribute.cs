using System;

namespace StructId;

/// <summary>
/// Attribute for marking a template type for the underlying value of struct id.
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class TValueAttribute : Attribute
{
}