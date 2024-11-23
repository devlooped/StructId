using Sample;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/{id}", (UserId id) => id);

app.Run();

readonly partial record struct UserId : IStructId;
