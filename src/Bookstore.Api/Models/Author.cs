namespace Bookstore.Api.Models;

public sealed class Author
{
    public int AuthorId { get; set; }

    public required string Name { get; set; }
}
