using Bookstore.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

app.MapControllers();

app.Run();
