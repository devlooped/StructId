using Microsoft.AspNetCore.Mvc;
using Sample;

namespace MvcWebApplication.Controllers;

public class HomeController : Controller
{
    [Route("/user/{id}")]
    public IActionResult GetUser(UserId? id)
    {
        if (id != null)
            return new JsonResult(new User(id.Value, Ipsum.GetPhrase(3)));

        return new NotFoundResult();
    }

    [Route("/product/{id}")]
    public IActionResult GetProduct(ProductId? id)
    {
        if (id != null)
            return new JsonResult(new Product(id.Value, Ipsum.GetPhrase(5)));

        return new NotFoundResult();
    }
}

public readonly partial record struct UserId : IStructId<Guid>;

public record User(UserId id, string Alias);