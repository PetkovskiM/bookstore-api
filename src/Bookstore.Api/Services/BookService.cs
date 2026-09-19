using Bookstore.Api.Contracts;
using Bookstore.Api.Data;
using Bookstore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Services;

public sealed class BookService(BookstoreDbContext database)
{
    public async Task<BookResponse> GetByIdAsync(int bookId, CancellationToken cancellationToken)
    {
        var book = await database.Books.AsNoTracking()
            .Include(book => book.Author)
            .SingleOrDefaultAsync(book => book.BookId == bookId, cancellationToken)
            ?? throw new BookNotFoundException();

        return ToResponse(book);
    }

    public async Task<BookResponse> CreateAsync(CreateBookRequest request, CancellationToken cancellationToken)
    {
        var book = new Book
        {
            Author = await ResolveAuthorAsync(request.Author!, cancellationToken),
            Title = request.Title!,
            SubTitle = request.SubTitle
        };

        database.Books.Add(book);
        // One save inserts a new author and book together, in EF's transaction.
        await database.SaveChangesAsync(cancellationToken);
        return ToResponse(book);
    }

    public async Task<BookResponse> ReplaceAsync(int bookId, BookWriteRequest request, CancellationToken cancellationToken)
    {
        var book = await database.Books.SingleOrDefaultAsync(
            book => book.BookId == bookId, cancellationToken)
            ?? throw new BookNotFoundException();

        // Resolve before changing the book, so an invalid reference changes nothing.
        book.Author = await ResolveAuthorAsync(request.Author!, cancellationToken);
        book.Title = request.Title!;
        book.SubTitle = request.SubTitle;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent DELETE can remove the book after our lookup.
            throw new BookNotFoundException();
        }

        return ToResponse(book);
    }

    public async Task DeleteAsync(int bookId, CancellationToken cancellationToken)
    {
        var deleted = await database.Books.Where(book => book.BookId == bookId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new BookNotFoundException();
        }
    }

    private async Task<Author> ResolveAuthorAsync(AuthorRequest request, CancellationToken cancellationToken)
    {
        var referencedAuthor = request.AuthorId is int authorId
            ? await database.Authors.SingleOrDefaultAsync(author => author.AuthorId == authorId, cancellationToken)
            : null;

        return AuthorRules.Resolve(request, referencedAuthor);
    }

    private static BookResponse ToResponse(Book book) =>
        new(book.BookId, new AuthorResponse(book.Author.AuthorId, book.Author.Name), book.Title, book.SubTitle);
}
