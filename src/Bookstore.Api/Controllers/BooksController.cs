using Bookstore.Api.Contracts;
using Bookstore.Api.Security;
using Bookstore.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookstore.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/books")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class BooksController(BookService books) : ControllerBase
{
    [HttpGet("search")]
    [Authorize(Policy = BookAuthorization.Search)]
    [ProducesResponseType<BookSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BookSearchResponse>> Search([FromQuery] BookSearchRequest request, CancellationToken cancellationToken)
    {
        return Ok(await books.SearchAsync(request, cancellationToken));
    }

    [HttpGet("{bookId:int}")]
    [Authorize(Policy = BookAuthorization.Manage)]
    [ProducesResponseType<BookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookResponse>> GetById(int bookId, CancellationToken cancellationToken)
    {
        return Ok(await books.GetByIdAsync(bookId, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = BookAuthorization.Manage)]
    [ProducesResponseType<BookResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookResponse>> Create([FromBody] CreateBookRequest request, CancellationToken cancellationToken)
    {
        var book = await books.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { bookId = book.BookId }, book);
    }

    [HttpPut("{bookId:int}")]
    [Authorize(Policy = BookAuthorization.Manage)]
    [ProducesResponseType<BookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookResponse>> Replace(int bookId, [FromBody] BookWriteRequest request, CancellationToken cancellationToken)
    {
        return Ok(await books.ReplaceAsync(bookId, request, cancellationToken));
    }

    [HttpDelete("{bookId:int}")]
    [Authorize(Policy = BookAuthorization.Manage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int bookId, CancellationToken cancellationToken)
    {
        await books.DeleteAsync(bookId, cancellationToken);
        return NoContent();
    }
}
