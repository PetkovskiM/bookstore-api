using Bookstore.Api.Data;
using Bookstore.Api.Errors;
using Bookstore.Api.Health;
using Bookstore.Api.OpenApi;
using Bookstore.Api.Security;
using Bookstore.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Authentication failures can contain token details at information/debug level.
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Warning);
builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration("Authentication")
    .Validate(settings => settings.IsValid(), "Configure an HTTPS Authentication:Authority, nonblank Authentication:Audience, and a valid optional Authentication:BackchannelHost. See README.md.")
    .ValidateOnStart();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
builder.Services.AddBookAuthorization();
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", timeout: TimeSpan.FromSeconds(5));
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen();
    builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
}
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

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.Use(async (context, next) =>
{
    if (!context.Request.IsHttps)
    {
        await Results.Problem(statusCode: 400, title: "HTTPS is required.").ExecuteAsync(context);
        return;
    }
    await next(context);
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Bookstore API v1");
        options.OAuthClientId("bookstore-browser");
        options.OAuthScopes(BookAuthorization.Search);
        options.ConfigObject.PersistAuthorization = false;
        options.ConfigObject.ValidatorUrl = null;
    });
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
