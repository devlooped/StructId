﻿// <auto-generated />
#nullable enable

using System;
using StructId;

[TStructId]
file partial record struct TSelf(IUtf8SpanFormattable Value) : IUtf8SpanFormattable
{
    /// <inheritdoc/>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider) 
        => ((IUtf8SpanFormattable)Value).TryFormat(utf8Destination, out bytesWritten, format, provider);
}