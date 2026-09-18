using Bookstore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bookstore.Api.Data;

public sealed class BookstoreDbContext(DbContextOptions<BookstoreDbContext> options)
    : DbContext(options)
{
    public DbSet<Author> Authors => Set<Author>();

    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var author = modelBuilder.Entity<Author>();
        author.ToTable("Authors", table => table.HasCheckConstraint(
            "CK_Authors_Name_MinLength", "LEN(LTRIM(RTRIM([Name]))) >= 3"));
        author.HasKey(value => value.AuthorId);
        author.Property(value => value.AuthorId).UseIdentityColumn();
        author.Property(value => value.Name).IsRequired().HasMaxLength(100);

        var book = modelBuilder.Entity<Book>();
        book.ToTable("Books", table => table.HasCheckConstraint(
            "CK_Books_Title_MinLength", "LEN(LTRIM(RTRIM([Title]))) >= 3"));
        book.HasKey(value => value.BookId);
        book.Property(value => value.BookId).UseIdentityColumn();
        book.Property(value => value.Title).IsRequired().HasMaxLength(100);
        book.Property(value => value.SubTitle).HasColumnType("nvarchar(max)").IsRequired(false);

        book.HasOne(value => value.Author)
            .WithMany(value => value.Books)
            .HasForeignKey(value => value.AuthorId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}
