namespace Bookstore.Api.Models;

public sealed class Book
{
    public int BookId { get; set; }

    public int AuthorId { get; set; }

    public required Author Author { get; set; }

    public required string Title { get; set; }

    public string? SubTitle { get; set; }
}
