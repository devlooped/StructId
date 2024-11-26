using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace StructId.Functional;

public readonly partial record struct ProductId : IStructId<Guid>;
public readonly partial record struct UserId : IStructId<long>;
public readonly partial record struct WalletId : IStructId;

public record Product(ProductId Id, string Name);
public record Wallet(WalletId Id, string Alias);
public record User(UserId Id, string Name, Wallet Wallet);

partial record struct ProductId
{
    public static implicit operator Guid(ProductId id) => id.Value;
    public static explicit operator ProductId(Guid value) => new(value);
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
            .UseSqlite("Data Source=ef.db")
            .UseStructId()
            .Options;

        using var context = new Context(options);

        // Seed data
        var productId = Guid.NewGuid();
        var product = new Product(new ProductId(productId), "Product");
        context.Products.Add(product);
        context.SaveChanges();

        var product2 = context.Products.First(x => productId == product.Id);
        Assert.Equal(product, product2);
    }

    public class Context : DbContext
    {
        public Context(DbContextOptions<Context> options) : base(options) { }
        public DbSet<Product> Products { get; set; } = null!;
        protected override void OnModelCreating(ModelBuilder model) => model.Entity<Product>().HasKey(e => e.Id);

        protected override void OnConfiguring(DbContextOptionsBuilder builder) => builder.UseStructId();
    }
}