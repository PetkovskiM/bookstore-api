namespace Bookstore.Api.Services;

public sealed class BookNotFoundException() : Exception("The requested book was not found.")
{
}

public sealed class AuthorNotFoundException() : Exception("The referenced author does not exist.")
{
}

public sealed class AuthorNameConflictException() : Exception("The author name must match the referenced author's stored name. Book requests cannot rename an existing author.")
{
}
