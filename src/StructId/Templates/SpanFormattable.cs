﻿// <auto-generated />
#nullable enable

using System;
using StructId;

[TStructId]
file partial record struct TSelf(ISpanFormattable Value) : ISpanFormattable
{
    /// <inheritdoc/>
    public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => ((ISpanFormattable)Value).TryFormat(destination, out charsWritten, format, provider);
}

file partial record struct TSelf
{
    // This partial is provided by Formattable template
    public string ToString(string? format, IFormatProvider? formatProvider) => throw new NotImplementedException();
}