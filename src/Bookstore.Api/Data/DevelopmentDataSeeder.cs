using Bookstore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Data;

internal static class DevelopmentDataSeeder
{
    // EF's command-line migration tools invoke the synchronous seeding hook.
    public static void Seed(BookstoreDbContext context)
    {
        if (context.Authors.Any() || context.Books.Any())
        {
            return;
        }

        context.Books.AddRange(CreateSampleBooks());
        context.SaveChanges();
    }

    public static async Task SeedAsync(
        BookstoreDbContext context, CancellationToken cancellationToken)
    {
        // Bootstrap an empty database only. Never refill or modify an existing catalog.
        if (await context.Authors.AnyAsync(cancellationToken)
            || await context.Books.AnyAsync(cancellationToken))
        {
            return;
        }

        context.Books.AddRange(CreateSampleBooks());
        await context.SaveChangesAsync(cancellationToken);
    }

    private static Book[] CreateSampleBooks()
    {
        var frankHerbert = new Author { Name = "Frank Herbert" };
        var ursulaLeGuin = new Author { Name = "Ursula K. Le Guin" };
        var janeAusten = new Author { Name = "Jane Austen" };

        return
        [
            new Book { Title = "Dune", Author = frankHerbert },
            new Book { Title = "Dune Messiah", Author = frankHerbert },
            new Book { Title = "The Left Hand of Darkness", Author = ursulaLeGuin },
            new Book { Title = "The Dispossessed", SubTitle = "An Ambiguous Utopia", Author = ursulaLeGuin },
            new Book { Title = "Pride and Prejudice", Author = janeAusten }
        ];
    }
}
