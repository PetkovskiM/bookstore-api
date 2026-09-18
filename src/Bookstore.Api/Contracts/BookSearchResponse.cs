namespace Bookstore.Api.Contracts;

public sealed record BookSearchResponse(IReadOnlyList<BookResponse> Items, int TotalCount, int PageNumber, int PageSize);
