using Bookstore.Auth.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Bookstore.Auth.Data;

public sealed class DevelopmentDataSeeder(
    AuthDbContext database,
    UserManager<IdentityUser> users,
    IOpenIddictApplicationManager applications,
    IConfiguration configuration,
    IOptions<AuthSettings> settings)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await database.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            // Serialize initializers across processes. The transaction releases the lock even on failure.
            await database.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource = N'Bookstore.Auth.DevelopmentSeed',
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
                IF @result < 0 THROW 50001, 'Could not acquire the authentication seed lock.', 1;
                """, cancellationToken);

            if (await applications.FindByClientIdAsync(OAuthContract.ManagementClientId, cancellationToken) is null)
            {
                var secret = RequiredSecret("DevelopmentDemo:ManagementClientSecret");
                await applications.CreateAsync(OAuthContract.ManagementClient(secret), cancellationToken);
            }

            if (await applications.FindByClientIdAsync(OAuthContract.BrowserClientId, cancellationToken) is null)
            {
                var redirectUris = settings.Value.BrowserRedirectUris;
                if (redirectUris.Length == 0 || !redirectUris.All(AuthSettings.IsHttpsAddress))
                {
                    throw new InvalidOperationException("Configure exact HTTPS Auth:BrowserRedirectUris. See README.md.");
                }
                await applications.CreateAsync(
                    OAuthContract.BrowserClient(redirectUris.Select(address => new Uri(address))), cancellationToken);
            }

            if (await users.FindByNameAsync(OAuthContract.DemoUserName) is null)
            {
                var user = new IdentityUser { UserName = OAuthContract.DemoUserName };
                var result = await users.CreateAsync(user, RequiredSecret("DevelopmentDemo:UserPassword"));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException("Demo user creation failed. Check the configured password policy.");
                }
            }

            await transaction.CommitAsync(cancellationToken);
        });
    }

    private string RequiredSecret(string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configure {key} using user secrets. See README.md.");
        }
        return value;
    }
}
