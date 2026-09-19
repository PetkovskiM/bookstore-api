using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Bookstore.Api.Security;

public sealed class ConfigureJwtBearerOptions(IOptions<JwtSettings> settings) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        var configuration = settings.Value;
        options.Authority = configuration.Authority;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.SaveToken = false;
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration.Authority,
            ValidateAudience = true,
            ValidAudience = configuration.Audience,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidTypes = ["at+jwt"]
        };
    }
}
