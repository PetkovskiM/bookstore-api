using System.Net;
using System.Net.Sockets;
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
        if (configuration.BackchannelHost is { } host)
        {
            var authority = new Uri(configuration.Authority);
            options.BackchannelHttpHandler = new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                ConnectCallback = async (context, cancellationToken) =>
                {
                    if (!string.Equals(context.DnsEndPoint.Host, authority.Host, StringComparison.OrdinalIgnoreCase)
                        || context.DnsEndPoint.Port != authority.Port)
                    {
                        throw new HttpRequestException("Discovery requests must use the configured authority.");
                    }
                    // Change only the TCP destination. HttpClient still verifies TLS for the
                    // original authority hostname, and token issuer/audience checks remain intact.
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(new DnsEndPoint(host, authority.Port), cancellationToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            };
        }
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
