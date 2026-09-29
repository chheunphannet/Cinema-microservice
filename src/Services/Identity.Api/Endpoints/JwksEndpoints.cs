using System.Security.Cryptography;
using Identity.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Endpoints;

public static class JwksEndpoints
{
    public static void MapJwksEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/.well-known/openid-configuration", (IConfiguration configuration) =>
        {
            var issuer = configuration["Jwt:Issuer"] ?? "cinemapos";
            var publicUrl = configuration["Gateway:PublicUrl"] ?? "http://identity-api:8080";
            
            return Results.Json(new
            {
                issuer = issuer,
                jwks_uri = publicUrl + "/.well-known/jwks.json",
                id_token_signing_alg_values_supported = new[] { "RS256" }
            });
        }).AllowAnonymous()
        .WithSummary("OpenID Connect Discovery");

        routes.MapGet("/.well-known/jwks.json", (IConfiguration configuration) =>
        {
            var jwks = new JsonWebKeySet();
            
            // Add current key (used for signing)
            var currentKey = RsaKeyProvider.GetKey();
            jwks.Keys.Add(JsonWebKeyConverter.ConvertFromRSASecurityKey(currentKey));

            // Add previous key if any (used for validating overlapping tokens during rotation)
            var previousPem = configuration["Jwt:PreviousPublicKeyPem"];
            if (!string.IsNullOrWhiteSpace(previousPem))
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(previousPem);
                var key = new RsaSecurityKey(rsa) { KeyId = "cinema-pos-auth-key-previous" };
                jwks.Keys.Add(JsonWebKeyConverter.ConvertFromRSASecurityKey(key));
            }

            return Results.Json(jwks);
        }).AllowAnonymous()
        .WithSummary("JSON Web Key Set (JWKS)");
    }
}
