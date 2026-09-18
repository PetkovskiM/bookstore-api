using System.ComponentModel.DataAnnotations;

namespace Bookstore.Api.Contracts;

public sealed class AuthorRequest
{
    private string? _name;

    [Range(1, int.MaxValue, ErrorMessage = "Author ID must be a positive integer when supplied.")]
    public int? AuthorId { get; set; }

    [Required(ErrorMessage = "Author name is required.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Author name must contain 3-100 characters after trimming.")]
    public string? Name
    {
        get => _name;
        set => _name = value?.Trim();
    }
}
