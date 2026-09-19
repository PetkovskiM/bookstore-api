using Bookstore.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Bookstore.Auth.Controllers;

public sealed class AccountController(SignInManager<IdentityUser> signIn) : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View();

    [HttpGet("/account/login")]
    public IActionResult Login(string returnUrl = "/")
    {
        if (!Url.IsLocalUrl(returnUrl))
        {
            return Problem(statusCode: 400, title: "The return URL must be local.");
        }
        return View(new LoginRequest { ReturnUrl = returnUrl });
    }

    [HttpPost("/account/login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (!Url.IsLocalUrl(request.ReturnUrl))
        {
            return Problem(statusCode: 400, title: "The return URL must be local.");
        }
        if (ModelState.IsValid)
        {
            var result = await signIn.PasswordSignInAsync(request.UserName, request.Password,
                isPersistent: false, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                return LocalRedirect(request.ReturnUrl);
            }
            ModelState.AddModelError(string.Empty, "Sign-in failed. Check your details and try again.");
        }
        request.Password = "";
        ModelState.Remove(nameof(request.Password));
        return View(request);
    }

    [HttpPost("/account/logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return LocalRedirect("/");
    }
}
