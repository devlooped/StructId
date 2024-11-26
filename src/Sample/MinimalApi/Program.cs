using Sample;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/user/{id}", (UserId id) => new User(id, Ipsum.GetPhrase(3)));
app.MapGet("/product/{id}", (ProductId id) => new Product(id, Ipsum.GetPhrase(5)));

app.Run();

readonly partial record struct UserId : IStructId<int>;

record User(UserId id, string Alias);