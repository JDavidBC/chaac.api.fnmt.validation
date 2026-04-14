using Chaac.Api.Fnmt.Validation.Models;

namespace Chaac.Api.Fnmt.Validation.Services;

/// <summary>
/// Creates signed JWT tokens from a verified certificate identity.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a signed JWT bearer token for the supplied certificate identity.
    /// </summary>
    /// <param name="identity">Identity information extracted from a validated certificate.</param>
    /// <returns>A <see cref="LoginResponse"/> containing the token and basic user info.</returns>
    LoginResponse GenerateToken(CertificateIdentity identity);
}
