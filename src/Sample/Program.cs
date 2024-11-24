using Microsoft.AspNetCore.Mvc;
using Sample;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/{id}", (UserId id) => new User(id, "kzu"));

app.Run();

readonly partial record struct UserId : IStructId<int>;

record User(UserId id, string Alias);