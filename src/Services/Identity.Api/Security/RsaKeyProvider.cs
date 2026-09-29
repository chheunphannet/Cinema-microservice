using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Security;

public static class RsaKeyProvider
{
    private static RSA? _rsa;
    private static RsaSecurityKey? _key;

    public static void Initialize(IConfiguration configuration)
    {
        _rsa = RSA.Create();
        
        var pemContent = configuration["Jwt:PrivateKeyPem"];
        var keyPath = configuration["Jwt:PrivateKeyPath"];
        
        if (!string.IsNullOrWhiteSpace(pemContent))
        {
            _rsa.ImportFromPem(pemContent);
        }
        else if (!string.IsNullOrWhiteSpace(keyPath) && File.Exists(keyPath))
        {
            var pem = File.ReadAllText(keyPath);
            _rsa.ImportFromPem(pem);
        }
        else
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (env != "Development")
            {
                throw new InvalidOperationException("JWT Private Key must be provided in production via 'Jwt:PrivateKeyPem' or 'Jwt:PrivateKeyPath'.");
            }
            
            // Generate ephemeral key for development if no path/pem specified.
            // We do not write to the application directory to support read-only containers.
            _rsa.KeySize = 2048;
        }

        _key = new RsaSecurityKey(_rsa) { KeyId = "cinema-pos-auth-key-1" };
    }

    public static RsaSecurityKey GetKey()
    {
        if (_key == null) throw new InvalidOperationException("RsaKeyProvider is not initialized.");
        return _key;
    }

    public static RSA GetRsa()
    {
        if (_rsa == null) throw new InvalidOperationException("RsaKeyProvider is not initialized.");
        return _rsa;
    }
}
