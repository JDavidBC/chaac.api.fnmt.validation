namespace Chaac.Api.Fnmt.Validation.Models;

/// <summary>
/// Response returned by the /login endpoint on successful certificate authentication.
/// </summary>
public sealed class LoginResponse
{
    /// <summary>Signed JWT bearer token.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>Token type (always "Bearer").</summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>Seconds until the token expires.</summary>
    public int ExpiresIn { get; init; }

    /// <summary>DNI or NIE of the authenticated user.</summary>
    public string DniNie { get; init; } = string.Empty;

    /// <summary>Full name from the certificate Common Name.</summary>
    public string Name { get; init; } = string.Empty;
}
