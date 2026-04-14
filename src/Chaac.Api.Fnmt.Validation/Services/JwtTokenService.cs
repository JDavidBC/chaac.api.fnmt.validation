using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Chaac.Api.Fnmt.Validation.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Chaac.Api.Fnmt.Validation.Services;

/// <summary>
/// Production implementation of <see cref="IJwtTokenService"/>.
/// Generates HS256-signed JWT tokens from a verified certificate identity.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <inheritdoc/>
    public LoginResponse GenerateToken(CertificateIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var keyBytes = Encoding.UTF8.GetBytes(_settings.Secret);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var issuedAt = DateTime.UtcNow;
        var expires = issuedAt.AddMinutes(_settings.ExpirationMinutes);

        var claims = new List<Claim>
        {
            // Standard JWT claims
            new(JwtRegisteredClaimNames.Sub, identity.DniNie),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),

            // Application-specific claims
            new("dni_nie", identity.DniNie),
            new("common_name", identity.CommonName),
            new("cert_serial", identity.SerialNumber),
            new("cert_issuer", identity.Issuer),
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = issuedAt,
            Expires = expires,
            SigningCredentials = credentials,
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        var tokenString = handler.WriteToken(token);

        return new LoginResponse
        {
            Token = tokenString,
            TokenType = "Bearer",
            ExpiresIn = _settings.ExpirationMinutes * 60,
            DniNie = identity.DniNie,
            Name = identity.CommonName,
        };
    }
}
