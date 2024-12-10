using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace StructId.Functional;

// showcases Ulid integration
public readonly partial record struct UlidId : IStructId<Ulid>;

public record UlidProduct(UlidId Id, string Name);

public class StringUlidHandler : Dapper.SqlMapper.TypeHandler<Ulid>
{
    public override Ulid Parse(object value)
    {
        return Ulid.Parse((string)value);
    }

    public override void SetValue(IDbDataParameter parameter, Ulid value)
    {
        parameter.DbType = DbType.StringFixedLength;
        parameter.Size = 26;
        parameter.Value = value.ToString();
    }
}

// showcases alternative serialization
//public class BinaryUlidHandler : TypeHandler<Ulid>
//{
//    public override Ulid Parse(object value)
//    {
//        return new Ulid((byte[])value);
//    }

//    public override void SetValue(IDbDataParameter parameter, Ulid value)
//    {
//        parameter.DbType = DbType.Binary;
//        parameter.Size = 16;
//        parameter.Value = value.ToByteArray();
//    }
//}

public class DapperTests
{
    [Fact]
    public void Dapper()
    {
        using var connection = new SqliteConnection("Data Source=dapper.db")
            .UseStructId();

        connection.Open();

        // Seed data
        var productId = Guid.NewGuid();
        var product = new Product(new ProductId(productId), "Product");

        connection.Execute("INSERT INTO Products (Id, Name) VALUES (@Id, @Name)", new Product(ProductId.New(), "Product1"));
        connection.Execute("INSERT INTO Products (Id, Name) VALUES (@Id, @Name)", product);
        connection.Execute("INSERT INTO Products (Id, Name) VALUES (@Id, @Name)", new Product(ProductId.New(), "Product2"));

        var product2 = connection.QueryFirst<Product>("SELECT * FROM Products WHERE Id = @Id", new { Id = productId });
        Assert.Equal(product, product2);
    }

    [Fact]
    public void DapperUlid()
    {
        using var connection = new SqliteConnection("Data Source=dapper.db")
            .UseStructId();

        connection.Open();

        // Seed data
        var productId = Ulid.NewUlid();
        var product = new UlidProduct(new UlidId(productId), "Product");

        connection.Execute("INSERT INTO Products (Id, Name) VALUES (@Id, @Name)", new UlidProduct(UlidId.New(), "Product1"));
        connection.Execute("INSERT INTO Products (Id, Name) VALUES (@Id, @Name)", product);
        connection.Execute("INSERT INTO Products (Id, Name) VALUES (@Id, @Name)", new UlidProduct(UlidId.New(), "Product2"));

        var product2 = connection.QueryFirst<UlidProduct>("SELECT * FROM Products WHERE Id = @Id", new { Id = productId });
        Assert.Equal(product, product2);
    }

}
