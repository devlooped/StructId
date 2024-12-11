using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using static StructId.Functional.FunctionalTests;

namespace StructId.Functional;

// showcases Ulid integration
public readonly partial record struct UlidId : IStructId<Ulid>;

public record UlidProduct(UlidId Id, string Name);

// showcases a custom Dapper type handler trumps the built-in support for 
// types that provide IParsable<T> and IFormattable.
public class StringUlidHandler : Dapper.SqlMapper.TypeHandler<Ulid>
{
    // To ensure in tests that this is used over the built-in templatized support 
    // due to Ulid implementing IParsable<T> and IFormattable.
    public static bool Used { get; private set; }

    public override Ulid Parse(object value)
    {
        Used = true;
        return Ulid.Parse((string)value, null);
    }

    public override void SetValue(IDbDataParameter parameter, Ulid value)
    {
        Used = true;
        parameter.DbType = DbType.StringFixedLength;
        parameter.Size = 26;
        parameter.Value = value.ToString(null, null);
    }
}

public partial class UlidToStringConverter : ValueConverter<Ulid, string>
{
    public UlidToStringConverter() : this(null) { }

    public UlidToStringConverter(ConverterMappingHints? mappingHints = null)
        : base(id => id.ToString(), value => Ulid.Parse(value), mappingHints) { }
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

public class UlidTests
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

        Assert.True(StringUlidHandler.Used);
    }

    [Fact]
    public void EntityFramework()
    {
        var options = new DbContextOptionsBuilder<UlidContext>()
            .UseStructId()
            .UseSqlite("Data Source=ef.db")
            // Uncomment to see full SQL being run
            // .EnableSensitiveDataLogging()
            // .UseLoggerFactory(new LoggerFactory(output))
            .Options;

        using var context = new UlidContext(options);

        var id = UlidId.New();
        var product = new UlidProduct(new UlidId(id), "Product");

        // Seed data
        context.Products.Add(new UlidProduct(UlidId.New(), "Product1"));
        context.Products.Add(product);
        context.Products.Add(new UlidProduct(UlidId.New(), "Product2"));

        context.SaveChanges();

        var product2 = context.Products.Where(x => x.Id == id).FirstOrDefault();
        Assert.Equal(product, product2);

        Ulid guid = id;

        var dict = new ConcurrentDictionary<string, int>(
            [
                new("foo", 1),
                new("bar", 2),
            ]);

        var product3 = context.Products.FirstOrDefault(x => guid == x.Id);
        Assert.Equal(product, product3);
    }

    public class UlidContext : DbContext
    {
        public UlidContext(DbContextOptions<UlidContext> options) : base(options) { }
        public DbSet<UlidProduct> Products { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UlidProduct>().Property(x => x.Id)
                //.HasConversion(new UlidToStringConverter())
                .HasConversion(new UlidId.EntityFrameworkUlidValueConverter());
            //.HasConversion(new UlidId.EntityFrameworkValueConverter());
        }
    }
}
