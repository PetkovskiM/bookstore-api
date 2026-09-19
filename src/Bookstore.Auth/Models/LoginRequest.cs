using System.ComponentModel.DataAnnotations;

namespace Bookstore.Auth.Models;

public sealed class LoginRequest
{
    [Required]
    [Display(Name = "Username")]
    public string UserName { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string ReturnUrl { get; set; } = "/";
}
