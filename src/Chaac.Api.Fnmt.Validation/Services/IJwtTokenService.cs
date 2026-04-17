using Chaac.Api.Fnmt.Validation.Models;

namespace Chaac.Api.Fnmt.Validation.Services;

/// <summary>
/// Creates signed JWT tokens from a verified certificate identity.
/// </summary>
public interface IJwtTokenService
{
    // Genera el token basado en nuestra nueva estructura de identidad
    LoginResponse GenerateToken(CertificateIdentity identity, string ip, string ciudad, string pais);
}
