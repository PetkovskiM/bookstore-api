using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bookstore.Auth.Data;

// Schema tooling needs database settings, not a running issuer, certificates, or demo credentials.
public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(AuthDbContextFactory).Assembly.GetName().Name
        });
        var connection = builder.Configuration.GetConnectionString("Authentication");
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("Configure ConnectionStrings:Authentication. See README.md.");
        }

        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer(connection, sql => sql.EnableRetryOnFailure())
            .UseOpenIddict()
            .Options;
        return new AuthDbContext(options);
    }
}
