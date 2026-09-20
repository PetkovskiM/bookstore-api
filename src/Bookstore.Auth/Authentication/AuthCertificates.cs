using System.Security.Cryptography.X509Certificates;

namespace Bookstore.Auth.Authentication;

public static class AuthCertificates
{
    public static void Configure(OpenIddictServerBuilder server, IHostEnvironment environment, IConfiguration configuration)
    {
        if (environment.IsDevelopment()
            && string.IsNullOrWhiteSpace(configuration["Certificates:Signing:Path"])
            && string.IsNullOrWhiteSpace(configuration["Certificates:Encryption:Path"]))
        {
            server.AddDevelopmentSigningCertificate().AddDevelopmentEncryptionCertificate();
            return;
        }

        // Production must receive separately provisioned credentials; never fall back to development keys.
        server.AddSigningCertificate(Load("Signing", environment, configuration));
        server.AddEncryptionCertificate(Load("Encryption", environment, configuration));
    }

    public static X509Certificate2 Load(string purpose, IHostEnvironment environment, IConfiguration configuration)
    {
        var path = configuration[$"Certificates:{purpose}:Path"];
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"Configure Certificates:{purpose}:Path. See README.md.");
        }
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            Path.GetFullPath(path, environment.ContentRootPath),
            configuration[$"Certificates:{purpose}:Password"], X509KeyStorageFlags.EphemeralKeySet);
        if (!certificate.HasPrivateKey || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow
            || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow)
        {
            certificate.Dispose();
            throw new InvalidOperationException($"Configure a valid {purpose.ToLowerInvariant()} certificate with its private key.");
        }
        return certificate;
    }
}
