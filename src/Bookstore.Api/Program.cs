using Bookstore.Api.Data;
using Bookstore.Api.Errors;
using Bookstore.Api.Services;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<BookService>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Instance = context.HttpContext.Request.Path.Value;
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddDbContext<BookstoreDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Bookstore");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Configure ConnectionStrings:Bookstore using user secrets or an environment variable. See README.md.");
    }

    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());

    if (builder.Environment.IsDevelopment())
    {
        // EF holds its migration lock while these hooks run, including on repeated startup.
        options.UseSeeding((context, _) => DevelopmentDataSeeder.Seed((BookstoreDbContext)context))
            .UseAsyncSeeding((context, _, cancellationToken) =>
                DevelopmentDataSeeder.SeedAsync((BookstoreDbContext)context, cancellationToken));
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<BookstoreDbContext>();
    await database.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // The handler logs safe diagnostics; prevent middleware logging raw exceptions.
    SuppressDiagnosticsCallback = _ => true
});
app.UseStatusCodePages(async context =>
{
    var httpContext = context.HttpContext;
    var factory = httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
    await Results.Problem(factory.CreateProblemDetails(httpContext, httpContext.Response.StatusCode))
        .ExecuteAsync(httpContext);
});

app.MapControllers();

app.Run();
