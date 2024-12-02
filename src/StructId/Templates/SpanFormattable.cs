﻿using System;
using StructId;

[TStructId]
file partial record struct SpanFormattable(ISpanFormattable Value) : ISpanFormattable
{
    public readonly string ToString(string? format, IFormatProvider? formatProvider)
        => Value.ToString(format, formatProvider);

    public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => this.Value.TryFormat(destination, out charsWritten, format, provider);
}