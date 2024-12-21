﻿using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using StructId;

// TODO: pending making it conditionally included at compile-time
[TValue]
file class TId_TypeHandler : Dapper.SqlMapper.TypeHandler<TValue>
{
    public override TValue Parse(object value) => TValue.Parse((string)value, null);

    public override void SetValue(IDbDataParameter parameter, TValue value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString(null, null);
    }
}

file partial struct TValue : IParsable<TValue>, IFormattable
{
    public static TValue Parse(string s, IFormatProvider? provider) => throw new NotImplementedException();
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TValue result) => throw new NotImplementedException();
    public string ToString(string? format, IFormatProvider? formatProvider) => throw new NotImplementedException();
}