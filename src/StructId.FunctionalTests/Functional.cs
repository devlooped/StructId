using System.Diagnostics.CodeAnalysis;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StructId.Functional;

// Showcases providing your own ctor
public readonly partial record struct ProductId(Guid Value) : IStructId<Guid>;

public readonly partial record struct UserId : IStructId<long>;
// Showcases string-based id
public readonly partial record struct WalletId : IStructId;

public record Product(ProductId Id, string Name);
public record Wallet(WalletId Id, string Alias);
public record User(UserId Id, string Name, Wallet Wallet);

public class FunctionalTests(ITestOutputHelper output)
{
    [Fact]
    public void EqualityTest()
    {
        var guid = Guid.NewGuid();
        var id1 = new ProductId(guid);
        var id2 = new ProductId(guid);

        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
    }

    [Fact]
    public void ImplicitAndExplicitCast()
    {
        var guid = Guid.NewGuid();
        var id = new ProductId(guid);
        Guid guid2 = id;
        var id2 = (ProductId)guid2;
        Assert.Equal(guid, guid2);
        Assert.Equal(id, id2);
    }

    [Fact]
    public void Newtonsoft()
    {
        var product = new Product(new ProductId(Guid.NewGuid()), "Product");
        var user = new User(new UserId(1), "User", new Wallet(new WalletId("1234"), "Wallet"));

        var json = JsonConvert.SerializeObject(product, Formatting.Indented);

        // Serialized as a primitive
        Assert.Equal(JTokenType.String, JObject.Parse(json).Property("Id")!.Value.Type);

        var product2 = JsonConvert.DeserializeObject<Product>(json);
        Assert.Equal(product, product2);

        json = JsonConvert.SerializeObject(user, Formatting.Indented);

        // Serialized as a primitive
        Assert.Equal(JTokenType.Integer, JObject.Parse(json).Property("Id")!.Value.Type);
        Assert.Equal(JTokenType.String, JObject.Parse(json).SelectToken("$.Wallet.Id")!.Type);

        var user2 = JsonConvert.DeserializeObject<User>(json);
        Assert.Equal(user, user2);
    }

    [Fact]
    public void EntityFramework()
    {
        var options = new DbContextOptionsBuilder<Context>()
            .UseStructId()
            .UseSqlite("Data Source=ef.db")
            // Uncomment to see full SQL being run
            // .EnableSensitiveDataLogging()
            // .UseLoggerFactory(new LoggerFactory(output))
            .Options;

        using var context = new Context(options);

        var id = ProductId.New();
        var product = new Product(new ProductId(id), "Product");

        // Seed data
        context.Products.Add(new Product(ProductId.New(), "Product1"));
        context.Products.Add(product);
        context.Products.Add(new Product(ProductId.New(), "Product2"));

        context.SaveChanges();

        var product2 = context.Products.Where(x => x.Id == id).FirstOrDefault();
        Assert.Equal(product, product2);

        Guid guid = id;

        var product3 = context.Products.FirstOrDefault(x => guid == x.Id);
        Assert.Equal(product, product3);
    }

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
    public void CustomTemplate()
    {
        var id = ProductId.New();
        Assert.IsAssignableFrom<IId>(id);
    }

    [Fact]
    public void SpanFormattableIdsImplementISpanFormattable()
    {
        Assert.IsAssignableFrom<ISpanFormattable>(ProductId.New());
        Assert.IsAssignableFrom<ISpanFormattable>(UserId.New(123));
    }

    [Fact]
    public void StringIdDoesNotImplementISpanFormattable()
    {
        Assert.IsNotAssignableFrom<ISpanFormattable>(WalletId.New("foo"));
    }

    [Fact]
    public void GuidImplementUtf8SpanFormattable()
    {
        var id = ProductId.New();

        Assert.IsAssignableFrom<IUtf8SpanFormattable>(id);

        Span<byte> utf8Destination = new byte[36]; // Typical GUID length in string form

        if (id.TryFormat(utf8Destination, out int bytesWritten, default, null))
        {
            var guid = new Guid(Encoding.UTF8.GetString(utf8Destination.Slice(0, bytesWritten)));
            Assert.Equal(id.Value, guid);
        }
        else
        {
            Assert.Fail("TryFormat failed");
        }
    }

    public class Context : DbContext
    {
        public Context(DbContextOptions<Context> options) : base(options) { }
        public DbSet<Product> Products { get; set; } = null!;
    }

    class LoggerFactory(ITestOutputHelper output) : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider) => throw new NotImplementedException();
        public ILogger CreateLogger(string categoryName) => new Logger(output);
        public void Dispose() { }

        class Logger(ITestOutputHelper output) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => output.WriteLine(formatter(state, exception));
        }
    }
}