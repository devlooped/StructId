using ConsoleDb;
using Dapper;
using Microsoft.Data.Sqlite;

SQLitePCL.Batteries.Init();

using var connection = new SqliteConnection("Data Source=dapper.db")
    .UseStructId();

connection.Open();

// Seed data
var productId = Ulid.NewUlid();
var product = new Product(new ProductId(productId), "Product");

connection.Execute("INSERT INTO Products (Id, Name) VALUES (@Id, @Name)", new Product(ProductId.New(), "Product1"));
connection.Execute("INSERT INTO Products (Id, Name) VALUES (@Id, @Name)", product);
connection.Execute("INSERT INTO Products (Id, Name) VALUES (@Id, @Name)", new Product(ProductId.New(), "Product2"));

// showcase we can query by the underlying ulid
var product2 = connection.QueryFirst<Product>("SELECT * FROM Products WHERE Id = @Id", new { Id = productId });
var product3 = connection.QueryFirst<Product>("SELECT * FROM Products WHERE Id = @Id", new { Id = new ProductId(productId) });

Console.WriteLine("Found saved product by value: " + product.Equals(product2));
Console.WriteLine("Found saved product by id: " + product.Equals(product3));

public readonly partial record struct ProductId : IStructId<Ulid>;

public record Product(ProductId Id, string Name);