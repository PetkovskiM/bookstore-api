using Bookstore.Auth.Authentication;
using Bookstore.Auth.Data;
using Bookstore.Auth.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var settings = builder.Configuration.GetSection("Auth").Get<AuthSettings>() ?? new();
if (!AuthSettings.IsHttpsAddress(settings.Issuer))
{
    throw new InvalidOperationException("Configure Auth:Issuer as an absolute HTTPS address. See README.md.");
}

// OpenIddict information/debug messages can contain protocol requests and token responses.
builder.Logging.AddFilter("OpenIddict", LogLevel.Warning);
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("Auth"));
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context => context.ProblemDetails.Instance = context.HttpContext.Request.Path.Value);
builder.Services.AddExceptionHandler<AuthExceptionHandler>();
builder.Services.AddDbContext<AuthDbContext>(options =>
{
    var connection = builder.Configuration.GetConnectionString("Authentication");
    if (string.IsNullOrWhiteSpace(connection))
    {
        throw new InvalidOperationException("Configure ConnectionStrings:Authentication. See README.md.");
    }
    options.UseSqlServer(connection, sql => sql.EnableRetryOnFailure()).UseOpenIddict();
});

builder.Services.AddIdentityCore<IdentityUser>(options =>
{
    options.Password.RequiredLength = 12;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddSignInManager();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-Bookstore.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/account/login";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = false;
});
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
builder.Services.AddAuthorization();
builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AuthDbContext>())
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(settings.Issuer));
        options.SetAuthorizationEndpointUris("/connect/authorize");
        options.SetTokenEndpointUris("/connect/token");
        options.AllowClientCredentialsFlow();
        options.AllowImplicitFlow();
        options.Configure(configuration =>
        {
            configuration.ResponseModes.Clear();
            configuration.ResponseModes.Add(OpenIddict.Abstractions.OpenIddictConstants.ResponseModes.Fragment);
        });
        options.RegisterScopes(OAuthContract.ManageScope, OAuthContract.SearchScope);
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        // Signed JWTs let the separate API validate using discovery's public signing keys.
        options.DisableAccessTokenEncryption();
        AuthCertificates.Configure(options, builder.Environment, builder.Configuration);
        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough();
    });

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<DevelopmentDataSeeder>();
}

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await database.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
    await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync(app.Lifetime.ApplicationStopping);
}

app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
    if (!context.Request.IsHttps)
    {
        await Results.Problem(statusCode: 400, title: "HTTPS is required.").ExecuteAsync(context);
        return;
    }
    await next(context);
});
app.UseStatusCodePages(async context =>
{
    var http = context.HttpContext;
    var problems = http.RequestServices.GetRequiredService<ProblemDetailsFactory>();
    await Results.Problem(problems.CreateProblemDetails(http, http.Response.StatusCode)).ExecuteAsync(http);
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
