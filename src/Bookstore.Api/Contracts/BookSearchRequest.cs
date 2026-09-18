using System.ComponentModel.DataAnnotations;

namespace Bookstore.Api.Contracts;

public sealed class BookSearchRequest
{
    private string? _title;
    private string? _author;

    public string? Title
    {
        get => _title;
        set => _title = NormalizeFilter(value);
    }

    public string? Author
    {
        get => _author;
        set => _author = NormalizeFilter(value);
    }

    [Range(1, int.MaxValue, ErrorMessage = "Page number must be at least 1.")]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 10;

    private static string? NormalizeFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
