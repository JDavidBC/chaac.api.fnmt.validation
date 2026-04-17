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

    public LoginResponse GenerateToken(CertificateIdentity identity, string ip, string ciudad, string pais)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var keyBytes = Encoding.UTF8.GetBytes(_settings.Secret);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var issuedAt = DateTime.UtcNow;
        var expires = issuedAt.AddMinutes(_settings.ExpirationMinutes);

        // MANTENEMOS TODO LO ANTERIOR Y AÑADIMOS LA GEO AL FINAL
        var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, identity.Dni),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new("dni", identity.Dni),
        new("nombre", identity.Nombre),
        new("apellidos", identity.Apellidos),
        new("nombre_completo", identity.NombreCompleto),
        new("raw_dn", identity.RawDn), // El que te había quitado por error
        
        // --- LOS AÑADIDOS DE AUDITORÍA ---
        new("user_ip", ip),
        new("ciudad", ciudad),
        new("pais", pais)
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

        return new LoginResponse
        {
            Token = handler.WriteToken(token),
            TokenType = "Bearer",
            ExpiresIn = _settings.ExpirationMinutes * 60,
            DniNie = identity.Dni,
            Name = identity.NombreCompleto // Usamos el nombre completo que ya viene parseado
        };
    }
}
