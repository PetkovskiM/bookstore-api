using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Bookstore.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Bookstore.UnitTests.Authentication;

public sealed class ApiTokenValidationTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("auth", true)]
    [InlineData("auth.internal", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("", false)]
    [InlineData("https://auth:7200/", false)]
    [InlineData("auth/path", false)]
    [InlineData("auth:7200", false)]
    public void BackchannelAcceptsOnlyAnOptionalHostName(string? host, bool valid)
    {
        Assert.Equal(valid, new JwtSettings
        {
            Authority = "https://localhost:7200/", BackchannelHost = host
        }.IsValid());
    }

    [Fact]
    public void ContainerDiscoveryRoutingPreservesPublicAuthorityAndTlsValidation()
    {
        var options = new JwtBearerOptions();
        new ConfigureJwtBearerOptions(Options.Create(new JwtSettings
        {
            Authority = "https://localhost:7200/", BackchannelHost = "auth"
        })).Configure(options);

        using var handler = Assert.IsType<SocketsHttpHandler>(options.BackchannelHttpHandler);
        Assert.Equal("https://localhost:7200/", options.Authority);
        Assert.Equal(options.Authority, options.TokenValidationParameters.ValidIssuer);
        Assert.True(options.RequireHttpsMetadata);
        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
        Assert.NotNull(handler.ConnectCallback);
        Assert.False(handler.AllowAutoRedirect);
    }

    // These tokens exercise validation rules only. Real OAuth issuance is verified separately.
    [Theory]
    [InlineData("valid", true)]
    [InlineData("issuer", false)]
    [InlineData("audience", false)]
    [InlineData("signature", false)]
    [InlineData("expired", false)]
    [InlineData("future", false)]
    [InlineData("missing-expiry", false)]
    [InlineData("type", false)]
    [InlineData("algorithm", false)]
    [InlineData("unsigned", false)]
    public void ValidatesAllTokenTrustBoundaries(string scenario, bool expectedValid)
    {
        using var trustedRsa = RSA.Create(2048);
        using var untrustedRsa = RSA.Create(2048);
        var trustedKey = new RsaSecurityKey(trustedRsa) { KeyId = "unit-test-trusted" };
        var untrustedKey = new RsaSecurityKey(untrustedRsa) { KeyId = "unit-test-untrusted" };
        var options = new JwtBearerOptions();
        new ConfigureJwtBearerOptions(Options.Create(new JwtSettings
        {
            Authority = "https://issuer.example/", Audience = "bookstore-api"
        })).Configure(options);
        options.TokenValidationParameters.IssuerSigningKey = trustedKey;
        var handler = new JwtSecurityTokenHandler { SetDefaultTimesOnTokenCreation = false };
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = scenario == "issuer" ? "https://other.example/" : "https://issuer.example/",
            Audience = scenario == "audience" ? "another-api" : "bookstore-api",
            Subject = new ClaimsIdentity([new Claim("sub", "test-client"), new Claim("scope", "books.manage")]),
            IssuedAt = now.AddMinutes(-10),
            NotBefore = scenario == "future" ? now.AddMinutes(5) : now.AddMinutes(-10),
            Expires = scenario == "missing-expiry" ? null : scenario == "expired" ? now.AddMinutes(-2) : now.AddMinutes(15),
            TokenType = scenario == "type" ? "JWT" : "at+jwt",
            SigningCredentials = scenario == "unsigned" ? null : new SigningCredentials(
                scenario == "signature" ? untrustedKey : trustedKey,
                scenario == "algorithm" ? SecurityAlgorithms.RsaSha512 : SecurityAlgorithms.RsaSha256)
        };
        var token = handler.CreateEncodedJwt(descriptor);

        var error = Record.Exception(() => handler.ValidateToken(token, options.TokenValidationParameters, out _));

        if (expectedValid) Assert.Null(error);
        else Assert.IsAssignableFrom<SecurityTokenException>(error);
    }

    [Theory]
    [InlineData("https://localhost:7200/", "bookstore-api", true)]
    [InlineData("http://localhost:5200/", "bookstore-api", false)]
    [InlineData("", "bookstore-api", false)]
    [InlineData("https://user:password@localhost/", "bookstore-api", false)]
    [InlineData("https://localhost/?token=value", "bookstore-api", false)]
    [InlineData("https://localhost/#fragment", "bookstore-api", false)]
    [InlineData("https://localhost/", " ", false)]
    public void RequiresHttpsAuthorityAndAudience(string authority, string audience, bool valid)
    {
        Assert.Equal(valid, new JwtSettings { Authority = authority, Audience = audience }.IsValid());
    }
}
