using Microsoft.AspNetCore.Mvc;

namespace Bookstore.Auth.Controllers;

public sealed class DemoController(IHostEnvironment environment) : Controller
{
    [HttpGet("/demo")]
    public IActionResult Index() => environment.IsDevelopment() ? View() : NotFound();

    [HttpGet("/demo/callback")]
    public IActionResult Callback() => environment.IsDevelopment() ? View() : NotFound();
}
