using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using StructId;

[TValue]
file class TId_TypeHandler : Dapper.SqlMapper.TypeHandler<TId>
{
    public override TId Parse(object value) => TId.Parse((string)value, null);

    public override void SetValue(IDbDataParameter parameter, TId value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString(null, null);
    }
}

file partial struct TId : IParsable<TId>, IFormattable
{
    public static TId Parse(string s, IFormatProvider? provider) => throw new NotImplementedException();
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out TId result) => throw new NotImplementedException();
    public string ToString(string? format, IFormatProvider? formatProvider) => throw new NotImplementedException();
}