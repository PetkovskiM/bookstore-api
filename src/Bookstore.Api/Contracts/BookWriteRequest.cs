using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Bookstore.Api.Contracts;

public class BookWriteRequest
{
    private string? _title;

    [Required(ErrorMessage = "Author is required.")]
    public AuthorRequest? Author { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must contain 3-100 characters after trimming.")]
    public string? Title
    {
        get => _title;
        set => _title = value?.Trim();
    }

    [JsonPropertyName("subTitle")]
    public string? SubTitle { get; set; }
}
