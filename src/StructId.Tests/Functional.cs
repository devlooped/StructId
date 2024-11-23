using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace StructId.Functional;

[TypeConverter(typeof(ProductId.TypeConverter))]
public readonly partial record struct ProductId : IStructId<Guid>
{
    public class TypeConverter : ParseableTypeConverter<ProductId, Guid>
    {
        protected override ProductId Create(Guid value) => new(value);
    }
}

public readonly partial record struct WalletId : IStructId
{
    public class TypeConverter : StructIdConverters.StringTypeConverter<WalletId>
    {
        protected override WalletId Create(string value) => new(value);
    }
}

public abstract class ParseableTypeConverter<TStruct, TValue> : TypeConverter
    where TStruct : IStructId<TValue>
    where TValue : struct, IParsable<TValue>
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value) => value switch
    {
        string s => Create(TValue.Parse(s, culture)),
        null => null,
        _ => throw new ArgumentException($"Cannot convert from {value} to {typeof(TStruct).Name}", nameof(value))
    };

    protected abstract TStruct Create(TValue value);

    public override object? ConvertTo(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string))
        {
            return value switch
            {
                TValue id => id.ToString(),
                TStruct id => id.Value.ToString(),
                null => null,
                _ => throw new ArgumentException($"Cannot convert {value} to string", nameof(value))
            };
        }

        throw new ArgumentException($"Cannot convert {value ?? "(null)"} to {destinationType}", nameof(destinationType));
    }
}

public class FunctionalTests
{
    [Fact]
    public void Test()
    {
        var guid = Guid.NewGuid();
        var id1 = new ProductId(guid);
        var id2 = new ProductId(guid);
        
        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
    }
}
